import json
import sys
import unittest
from pathlib import Path

SCRIPT_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPT_ROOT))
from create_apply_approval import APPROVAL_PHRASE, create_approval  # noqa: E402


class CreateApplyApprovalTests(unittest.TestCase):
    def test_approval_derives_canonical_path_and_plan_bindings(self) -> None:
        from tempfile import TemporaryDirectory

        with TemporaryDirectory() as directory:
            tmp_path = Path(directory)
            plan = tmp_path / "session.plan.json"
            apply = tmp_path / "session.apply"
            plan.write_text(
                json.dumps(
                    {
                        "version": 2,
                        "snapshotFingerprint": "fingerprint",
                        "reviewVersion": "review",
                        "targetPrefabAssetPath": "Assets/UI/Main.prefab",
                        "prefabAssetPath": "Assets/UI/Main.prefab",
                    }
                ),
                encoding="utf-8",
            )

            approval = create_approval(str(plan), str(apply))

            self.assertEqual(approval["approvalText"], APPROVAL_PHRASE)
            self.assertEqual(approval["planPath"], str(plan.resolve()))
            self.assertTrue(approval["planSha256"])
            self.assertEqual(approval["snapshotFingerprint"], "fingerprint")
            self.assertEqual(json.loads(apply.read_text(encoding="utf-8")), approval)


    def test_existing_apply_requires_new_session(self) -> None:
        from tempfile import TemporaryDirectory

        with TemporaryDirectory() as directory:
            tmp_path = Path(directory)
            plan = tmp_path / "session.plan.json"
            apply = tmp_path / "session.apply"
            plan.write_text(
                json.dumps(
                    {
                        "version": 2,
                        "snapshotFingerprint": "fingerprint",
                        "reviewVersion": "review",
                        "targetPrefabAssetPath": "Assets/UI/Main.prefab",
                        "prefabAssetPath": "Assets/UI/Main.prefab",
                    }
                ),
                encoding="utf-8",
            )
            apply.write_text("{}", encoding="utf-8")

            with self.assertRaises(FileExistsError):
                create_approval(str(plan), str(apply))
