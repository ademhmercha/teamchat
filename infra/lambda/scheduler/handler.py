"""Arrete et demarre l'EC2 et la base RDS de TeamChat (economie la nuit).

Evenement : {"action": "start" | "stop" | "enforce"}

- stop    : arrete l'EC2 (puis la base RDS) portant le tag AutoSchedule=true.
- start   : demarre la base RDS, attend qu'elle soit disponible, puis demarre l'EC2
            (l'API a besoin de la base pour appliquer ses migrations au demarrage).
- enforce : RDS redemarre toute seule apres 7 jours d'arret ; hors des heures d'activite,
            re-arrete les bases RDS taguees qui tournent. Ne touche jamais a l'EC2.

Variables d'environnement : SNS_TOPIC_ARN, TZ_NAME, START_HOUR, STOP_HOUR, RDS_WAIT_SECONDS.
"""

import json
import logging
import os
import time
from datetime import datetime
from zoneinfo import ZoneInfo

import boto3
from botocore.exceptions import ClientError

logger = logging.getLogger()
logger.setLevel(logging.INFO)

TAG_KEY = "AutoSchedule"
TAG_VALUE = "true"
POLL_SECONDS = 20
SAFETY_MARGIN_SECONDS = 45  # on s'arrete avant la fin du delai de la Lambda


def _clients():
    """Point d'injection pour les tests."""
    return boto3.client("ec2"), boto3.client("rds"), boto3.client("sns")


# ---------------------------------------------------------------- decouverte


def _tagged_instances(ec2):
    """[(id, etat)] des instances EC2 taguees AutoSchedule=true (hors terminees)."""
    found = []
    paginator = ec2.get_paginator("describe_instances")
    pages = paginator.paginate(
        Filters=[
            {"Name": f"tag:{TAG_KEY}", "Values": [TAG_VALUE]},
            {"Name": "instance-state-name", "Values": ["pending", "running", "stopping", "stopped"]},
        ]
    )
    for page in pages:
        for reservation in page["Reservations"]:
            for instance in reservation["Instances"]:
                found.append((instance["InstanceId"], instance["State"]["Name"]))
    return found


def _tagged_databases(rds):
    """[(id, statut)] des bases RDS taguees AutoSchedule=true (RDS ne sait pas filtrer par tag)."""
    found = []
    paginator = rds.get_paginator("describe_db_instances")
    for page in paginator.paginate():
        for db in page["DBInstances"]:
            tags = rds.list_tags_for_resource(ResourceName=db["DBInstanceArn"])["TagList"]
            if any(t["Key"] == TAG_KEY and t["Value"] == TAG_VALUE for t in tags):
                found.append((db["DBInstanceIdentifier"], db["DBInstanceStatus"]))
    return found


# ------------------------------------------------------------------- actions


def _stop_ec2(ec2, result):
    ids = []
    for instance_id, state in _tagged_instances(ec2):
        if state in ("running", "pending"):
            ids.append(instance_id)
        else:
            result["skipped"].append(f"ec2:{instance_id} ({state})")
    if ids:
        ec2.stop_instances(InstanceIds=ids)
        result["done"].extend(f"ec2:{i} stop" for i in ids)


def _stop_rds(rds, result, only_when_available=False):
    for db_id, status in _tagged_databases(rds):
        if status == "available":
            rds.stop_db_instance(DBInstanceIdentifier=db_id)
            result["done"].append(f"rds:{db_id} stop")
        else:
            result["skipped"].append(f"rds:{db_id} ({status})")


def _start_rds_and_wait(rds, result, wait_seconds):
    """Demarre les bases arretees puis attend qu'elles soient 'available' (dans la limite du temps)."""
    started = set()
    deadline = time.monotonic() + wait_seconds
    while True:
        pending = False
        for db_id, status in _tagged_databases(rds):
            if status == "stopped" and db_id not in started:
                rds.start_db_instance(DBInstanceIdentifier=db_id)
                started.add(db_id)
                result["done"].append(f"rds:{db_id} start")
                pending = True
            elif status != "available":
                pending = True  # starting, stopping (a redemarrer ensuite), configuring-...
        if not pending:
            return True
        if time.monotonic() + POLL_SECONDS >= deadline:
            return False
        time.sleep(POLL_SECONDS)


def _start_ec2(ec2, result):
    ids = []
    for instance_id, state in _tagged_instances(ec2):
        if state == "stopped":
            ids.append(instance_id)
        else:
            result["skipped"].append(f"ec2:{instance_id} ({state})")
    if ids:
        ec2.start_instances(InstanceIds=ids)
        result["done"].extend(f"ec2:{i} start" for i in ids)


def is_active_hour(now, start_hour, stop_hour):
    """Vrai pendant les heures d'activite [start_hour, stop_hour) (gere le passage de minuit)."""
    hour = now.hour
    if start_hour < stop_hour:
        return start_hour <= hour < stop_hour
    return hour >= start_hour or hour < stop_hour


# ------------------------------------------------------------------- handler


def handler(event, context):
    action = (event or {}).get("action")
    if action not in ("start", "stop", "enforce"):
        raise ValueError(f"Action inconnue : {action!r} (attendu : start, stop ou enforce)")

    ec2, rds, sns = _clients()
    result = {"action": action, "done": [], "skipped": [], "errors": []}

    try:
        if action == "stop":
            _stop_ec2(ec2, result)
            _stop_rds(rds, result)
        elif action == "start":
            budget = int(os.environ.get("RDS_WAIT_SECONDS", "600"))
            remaining = context.get_remaining_time_in_millis() / 1000 - SAFETY_MARGIN_SECONDS
            ready = _start_rds_and_wait(rds, result, max(0, min(budget, remaining)))
            if not ready:
                # On demarre quand meme l'EC2 : le conteneur retente tout seul (restart unless-stopped).
                result["errors"].append("RDS pas encore disponible : EC2 demarree quand meme")
            _start_ec2(ec2, result)
        else:  # enforce
            tz = ZoneInfo(os.environ.get("TZ_NAME", "UTC"))
            now = datetime.now(tz)
            if is_active_hour(now, int(os.environ.get("START_HOUR", "8")), int(os.environ.get("STOP_HOUR", "23"))):
                logger.info("enforce : heures d'activite, rien a faire")
                return result
            _stop_rds(rds, result)
    except ClientError as exc:
        logger.exception("Erreur AWS pendant %s", action)
        result["errors"].append(str(exc))

    logger.info(json.dumps(result))
    _notify(sns, result)

    if result["errors"]:
        raise RuntimeError("; ".join(result["errors"]))
    return result


def _notify(sns, result):
    topic = os.environ.get("SNS_TOPIC_ARN")
    # 'enforce' tourne toutes les heures : on ne previent que s'il a vraiment agi.
    if not topic or (result["action"] == "enforce" and not result["done"] and not result["errors"]):
        return
    status = "ERREUR" if result["errors"] else "OK"
    try:
        sns.publish(
            TopicArn=topic,
            Subject=f"TeamChat scheduler : {result['action']} {status}",
            Message=json.dumps(result, indent=2),
        )
    except ClientError:
        logger.exception("Notification SNS impossible")
