import os
import unittest
from datetime import datetime
from unittest import mock
from zoneinfo import ZoneInfo

from botocore.exceptions import ClientError

import handler

class FakeContext:
    def __init__(self, ms=840_000):
        self._ms = ms

    def get_remaining_time_in_millis(self):
        return self._ms

class FakeEc2:
    def __init__(self, instances, tag="true"):
        self.instances = dict(instances)
        self.tag = tag
        self.stopped, self.started = [], []

    def get_paginator(self, _):
        outer = self

        class P:
            def paginate(self, Filters):
                if outer.tag is None:
                    return [{"Reservations": []}]
                tags = [{"Key": "AutoSchedule", "Value": outer.tag}]
                return [{
                    "Reservations": [{
                        "Instances": [
                            {"InstanceId": i, "State": {"Name": s}, "Tags": tags}
                            for i, s in outer.instances.items()
                        ]
                    }]
                }]

        return P()

    def stop_instances(self, InstanceIds):
        self.stopped.extend(InstanceIds)

    def start_instances(self, InstanceIds):
        self.started.extend(InstanceIds)

class FakeRds:

    def __init__(self, status, tagged=True, after_start=("starting", "available"), tag="true"):
        self.status = status
        self.tagged = tagged
        self.tag = tag
        self.after_start = list(after_start)
        self.transitions = []
        self.calls = []

    def get_paginator(self, _):
        outer = self

        class P:
            def paginate(self):
                if outer.transitions:
                    outer.status = outer.transitions.pop(0)
                return [{"DBInstances": [{
                    "DBInstanceIdentifier": "teamchat-prod",
                    "DBInstanceArn": "arn:aws:rds:eu-north-1:123:db:teamchat-prod",
                    "DBInstanceStatus": outer.status,
                }]}]

        return P()

    def list_tags_for_resource(self, ResourceName):
        return {"TagList": [{"Key": "AutoSchedule", "Value": self.tag}] if self.tagged else []}

    def start_db_instance(self, DBInstanceIdentifier):
        self.calls.append("start")
        self.status = "starting"
        self.transitions = list(self.after_start)

    def stop_db_instance(self, DBInstanceIdentifier):
        self.calls.append("stop")
        self.status = "stopping"

class FakeSns:
    def __init__(self):
        self.published = []

    def publish(self, **kwargs):
        self.published.append(kwargs)

ENV = {"SNS_TOPIC_ARN": "arn:aws:sns:eu-north-1:123:alerts", "TZ_NAME": "Africa/Lagos",
       "START_HOUR": "8", "STOP_HOUR": "23", "RDS_WAIT_SECONDS": "600"}

class FakeTime:

    def __init__(self):
        self.now = 0.0

    def monotonic(self):
        return self.now

    def sleep(self, seconds):
        self.now += seconds

def run(event, ec2, rds, sns=None, ctx=None):
    sns = sns or FakeSns()
    with mock.patch.dict(os.environ, ENV), \
         mock.patch.object(handler, "_clients", return_value=(ec2, rds, sns)), \
         mock.patch.object(handler, "time", FakeTime()):
        return handler.handler(event, ctx or FakeContext()), sns

class StopTests(unittest.TestCase):
    def test_stop_arrete_ec2_et_rds(self):
        ec2, rds = FakeEc2({"i-1": "running"}), FakeRds("available")
        result, sns = run({"action": "stop"}, ec2, rds)
        self.assertEqual(ec2.stopped, ["i-1"])
        self.assertEqual(rds.calls, ["stop"])
        self.assertEqual(len(sns.published), 1)
        self.assertIn("stop OK", sns.published[0]["Subject"])

    def test_stop_ignore_ce_qui_est_deja_arrete(self):
        ec2, rds = FakeEc2({"i-1": "stopped"}), FakeRds("stopped")
        result, _ = run({"action": "stop"}, ec2, rds)
        self.assertEqual(ec2.stopped, [])
        self.assertEqual(rds.calls, [])
        self.assertEqual(len(result["skipped"]), 2)

    def test_ressource_non_taguee_est_ignoree(self):
        rds = FakeRds("available", tagged=False)
        ec2 = FakeEc2({"i-1": "running"})
        run({"action": "stop"}, ec2, rds)
        self.assertEqual(rds.calls, [])
        self.assertEqual(ec2.stopped, ["i-1"])

class SuspensionTests(unittest.TestCase):

    def test_stop_ignore_les_ressources_suspendues(self):
        ec2, rds = FakeEc2({"i-1": "running"}, tag="false"), FakeRds("available", tag="false")
        result, _ = run({"action": "stop"}, ec2, rds)
        self.assertEqual(ec2.stopped, [])
        self.assertEqual(rds.calls, [])
        self.assertEqual(result["errors"], [])
        self.assertTrue(any("suspendu" in item for item in result["skipped"]))

    def test_start_ignore_les_ressources_suspendues(self):
        ec2, rds = FakeEc2({"i-1": "stopped"}, tag="false"), FakeRds("stopped", tag="false")
        run({"action": "start"}, ec2, rds)
        self.assertEqual(ec2.started, [])
        self.assertEqual(rds.calls, [])

    def test_valeur_du_tag_insensible_a_la_casse(self):
        ec2, rds = FakeEc2({"i-1": "running"}, tag="True"), FakeRds("available", tag="TRUE")
        run({"action": "stop"}, ec2, rds)
        self.assertEqual(ec2.stopped, ["i-1"])
        self.assertEqual(rds.calls, ["stop"])

    def test_rien_de_tague_est_une_erreur_visible(self):
        ec2, rds = FakeEc2({"i-1": "running"}, tag=None), FakeRds("available", tagged=False)
        sns = FakeSns()
        with self.assertRaises(RuntimeError):
            run({"action": "stop"}, ec2, rds, sns=sns)
        self.assertEqual(ec2.stopped, [])
        self.assertEqual(rds.calls, [])
        self.assertIn("ERREUR", sns.published[0]["Subject"])

class StartTests(unittest.TestCase):
    def test_start_demarre_rds_puis_ec2_quand_la_base_est_disponible(self):
        ec2, rds = FakeEc2({"i-1": "stopped"}), FakeRds("stopped")
        order = []
        original_start = ec2.start_instances
        ec2.start_instances = lambda InstanceIds: (order.append(("ec2", rds.status)), original_start(InstanceIds))
        result, _ = run({"action": "start"}, ec2, rds)
        self.assertEqual(rds.calls, ["start"])
        self.assertEqual(ec2.started, ["i-1"])
        self.assertEqual(order, [("ec2", "available")])
        self.assertEqual(result["errors"], [])

    def test_start_si_la_base_ne_devient_pas_disponible_demarre_quand_meme_et_signale(self):
        ec2, rds = FakeEc2({"i-1": "stopped"}), FakeRds("stopped", after_start=["starting"] * 50)
        with self.assertRaises(RuntimeError):
            run({"action": "start"}, ec2, rds, ctx=FakeContext(ms=45_000 + 100_000))
        self.assertEqual(ec2.started, ["i-1"])

    def test_start_ne_touche_pas_une_instance_deja_en_marche(self):
        ec2, rds = FakeEc2({"i-1": "running"}), FakeRds("available")
        run({"action": "start"}, ec2, rds)
        self.assertEqual(ec2.started, [])
        self.assertEqual(rds.calls, [])

class EnforceTests(unittest.TestCase):
    def _at(self, hour):
        fixed = datetime(2026, 1, 15, hour, 0, tzinfo=ZoneInfo("Africa/Lagos"))
        return mock.patch.object(handler, "datetime", mock.Mock(now=mock.Mock(return_value=fixed)))

    def test_enforce_rearrete_la_base_redemarree_hors_des_heures(self):
        rds = FakeRds("available")
        with self._at(3):
            result, sns = run({"action": "enforce"}, FakeEc2({"i-1": "running"}), rds)
        self.assertEqual(rds.calls, ["stop"])
        self.assertEqual(len(sns.published), 1)

    def test_enforce_ne_touche_a_rien_pendant_les_heures_d_activite(self):
        rds = FakeRds("available")
        with self._at(12):
            _, sns = run({"action": "enforce"}, FakeEc2({}), rds)
        self.assertEqual(rds.calls, [])
        self.assertEqual(sns.published, [])

    def test_enforce_ignore_une_base_suspendue(self):
        rds = FakeRds("available", tag="false")
        with self._at(3):
            result, sns = run({"action": "enforce"}, FakeEc2({"i-1": "running"}, tag="false"), rds)
        self.assertEqual(rds.calls, [])
        self.assertEqual(sns.published, [])

    def test_enforce_ne_touche_jamais_a_l_ec2_et_reste_silencieux_si_rien_a_faire(self):
        ec2, rds = FakeEc2({"i-1": "running"}), FakeRds("stopped")
        with self._at(3):
            _, sns = run({"action": "enforce"}, ec2, rds)
        self.assertEqual(ec2.stopped, [])
        self.assertEqual(sns.published, [])

class MiscTests(unittest.TestCase):
    def test_action_inconnue(self):
        with self.assertRaises(ValueError):
            run({"action": "reboot"}, FakeEc2({}), FakeRds("available"))

    def test_heures_d_activite(self):
        d = lambda h: datetime(2026, 1, 1, h)
        self.assertTrue(handler.is_active_hour(d(8), 8, 23))
        self.assertTrue(handler.is_active_hour(d(22), 8, 23))
        self.assertFalse(handler.is_active_hour(d(23), 8, 23))
        self.assertFalse(handler.is_active_hour(d(7), 8, 23))
        self.assertTrue(handler.is_active_hour(d(2), 20, 6))
        self.assertFalse(handler.is_active_hour(d(12), 20, 6))

    def test_erreur_aws_est_signalee_par_sns_puis_relancee(self):
        ec2 = FakeEc2({"i-1": "running"})
        ec2.stop_instances = mock.Mock(side_effect=ClientError({"Error": {"Code": "UnauthorizedOperation", "Message": "x"}}, "StopInstances"))
        with self.assertRaises(RuntimeError):
            _, sns = run({"action": "stop"}, ec2, FakeRds("available"))
        sns = FakeSns()
        with self.assertRaises(RuntimeError):
            run({"action": "stop"}, ec2, FakeRds("available"), sns=sns)
        self.assertIn("ERREUR", sns.published[0]["Subject"])

if __name__ == "__main__":
    unittest.main()
