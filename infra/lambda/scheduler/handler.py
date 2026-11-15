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
SAFETY_MARGIN_SECONDS = 45

def _clients():
    return boto3.client("ec2"), boto3.client("rds"), boto3.client("sns")

def _instances(ec2):
    found = []
    paginator = ec2.get_paginator("describe_instances")
    pages = paginator.paginate(
        Filters=[
            {"Name": "tag-key", "Values": [TAG_KEY]},
            {"Name": "instance-state-name", "Values": ["pending", "running", "stopping", "stopped"]},
        ]
    )
    for page in pages:
        for reservation in page["Reservations"]:
            for instance in reservation["Instances"]:
                tags = {t["Key"]: t["Value"] for t in instance.get("Tags", [])}
                found.append((instance["InstanceId"], instance["State"]["Name"], tags.get(TAG_KEY)))
    return found

def _databases(rds):
    found = []
    paginator = rds.get_paginator("describe_db_instances")
    for page in paginator.paginate():
        for db in page["DBInstances"]:
            tags = rds.list_tags_for_resource(ResourceName=db["DBInstanceArn"])["TagList"]
            value = next((t["Value"] for t in tags if t["Key"] == TAG_KEY), None)
            if value is not None:
                found.append((db["DBInstanceIdentifier"], db["DBInstanceStatus"], value))
    return found

def _is_active(tag_value):
    return (tag_value or "").strip().lower() == TAG_VALUE

def _tagged_instances(ec2):
    return [(i, state) for i, state, value in _instances(ec2) if _is_active(value)]

def _tagged_databases(rds):
    return [(i, status) for i, status, value in _databases(rds) if _is_active(value)]

def _audit(ec2, rds, result):
    instances, databases = _instances(ec2), _databases(rds)
    for kind, items in (("ec2", instances), ("rds", databases)):
        for resource_id, _, value in items:
            if not _is_active(value):
                result["skipped"].append(f"{kind}:{resource_id} (suspendu : {TAG_KEY}={value})")
    if not instances and not databases:
        result["errors"].append(
            f"Aucune ressource ne porte le tag {TAG_KEY} : rien n'a ete fait "
            f"(tag manquant sur l'EC2 et sur RDS ? ajoute {TAG_KEY}={TAG_VALUE})"
        )

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
                pending = True
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
    hour = now.hour
    if start_hour < stop_hour:
        return start_hour <= hour < stop_hour
    return hour >= start_hour or hour < stop_hour

class _NothingTagged(Exception):
    pass

def handler(event, context):
    action = (event or {}).get("action")
    if action not in ("start", "stop", "enforce"):
        raise ValueError(f"Action inconnue : {action!r} (attendu : start, stop ou enforce)")

    ec2, rds, sns = _clients()
    result = {"action": action, "done": [], "skipped": [], "errors": []}

    try:
        _audit(ec2, rds, result)
        if result["errors"]:
            raise _NothingTagged()
        if action == "stop":
            _stop_ec2(ec2, result)
            _stop_rds(rds, result)
        elif action == "start":
            budget = int(os.environ.get("RDS_WAIT_SECONDS", "600"))
            remaining = context.get_remaining_time_in_millis() / 1000 - SAFETY_MARGIN_SECONDS
            ready = _start_rds_and_wait(rds, result, max(0, min(budget, remaining)))
            if not ready:
                result["errors"].append("RDS pas encore disponible : EC2 demarree quand meme")
            _start_ec2(ec2, result)
        else:
            tz = ZoneInfo(os.environ.get("TZ_NAME", "UTC"))
            now = datetime.now(tz)
            if is_active_hour(now, int(os.environ.get("START_HOUR", "8")), int(os.environ.get("STOP_HOUR", "23"))):
                logger.info("enforce : heures d'activite, rien a faire")
                return result
            _stop_rds(rds, result)
    except _NothingTagged:
        pass
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
