import json
import sys
import unittest
from pathlib import Path

SCRIPT_ROOT = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(SCRIPT_ROOT))
from create_apply_approval import APPROVAL_PHRASE, create_approval  # noqa: E402


class CreateApplyApprovalTests(unittest.TestCase):
    def test_prewrite_rejection_allows_new_approval_and_preserves_receipt(self) -> None:
        from tempfile import TemporaryDirectory

        with TemporaryDirectory() as directory:
            plan = Path(directory) / "session.plan.json"
            apply = Path(directory) / "session.apply"
            result = Path(str(apply) + "-result.json")
            plan.write_text(json.dumps({
                "version": 2, "snapshotFingerprint": "fingerprint", "reviewVersion": "revised",
                "targetPrefabAssetPath": "Assets/UI/Main.prefab", "preserveRequirements": ["keep visuals"],
            }), encoding="utf-8")
            for stage in ("approval", "binding", "naming", "prepare", "preflight", "preflight-cleanup"):
                receipt = json.dumps({"status": "rejected", "stage": stage})
                result.write_text(receipt, encoding="utf-8")
                approval = create_approval(str(plan), str(apply))
                self.assertEqual(approval["reviewVersion"], "revised")
                self.assertEqual(result.read_text(encoding="utf-8"), receipt)
                apply.unlink()

    def test_written_uncertain_and_unreadable_receipts_block_retry(self) -> None:
        from tempfile import TemporaryDirectory

        with TemporaryDirectory() as directory:
            plan = Path(directory) / "session.plan.json"
            apply = Path(directory) / "session.apply"
            result = Path(str(apply) + "-result.json")
            plan.write_text("{}", encoding="utf-8")
            for status in ("applied", "partial", "uncertain", "unknown"):
                result.write_text(json.dumps({"status": status, "stage": "verify"}), encoding="utf-8")
                with self.assertRaises(FileExistsError):
                    create_approval(str(plan), str(apply))
                self.assertFalse(apply.exists())
            result.write_text("{", encoding="utf-8")
            with self.assertRaises(json.JSONDecodeError):
                create_approval(str(plan), str(apply))
            self.assertFalse(apply.exists())

    def test_execution_markers_and_malformed_metadata_block_approval(self) -> None:
        from tempfile import TemporaryDirectory

        with TemporaryDirectory() as directory:
            plan = Path(directory) / "session.plan.json"
            apply = Path(directory) / "session.apply"
            plan.write_text(json.dumps({
                "version": 2, "snapshotFingerprint": "fingerprint", "reviewVersion": "review",
                "targetPrefabAssetPath": "Assets/UI/Main.prefab",
                "preserveRequirements": [{"operation": "unexpected"}],
            }), encoding="utf-8")
            for suffix in (".applying", ".applied", ".apply-uncertain", ".apply-failed"):
                marker = apply.with_suffix(suffix)
                marker.write_text("{}", encoding="utf-8")
                with self.assertRaises(FileExistsError):
                    create_approval(str(plan), str(apply))
                marker.unlink()
            with self.assertRaisesRegex(ValueError, "preserveRequirements"):
                create_approval(str(plan), str(apply))
            self.assertFalse(apply.exists())

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
