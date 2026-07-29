# PSD Explicit Import Modes

## Goal

Make full Prefab generation and structure-preserving incremental updates separate, visible actions in the PSD Inspector. An incremental action must never degrade into a full overwrite.

## User Interface

- `Full Generate Prefab` is always available. It is the explicit destructive regeneration path and retains the existing missing-profile recovery confirmation.
- `Incremental Update` is rendered only when the selected PSD has an exact, valid cleanup replay Profile for the resolved target Prefab.
- If the Profile is absent, stale, has no replay stages, has a different target path, or has a different target GUID, the incremental action is not rendered.

## Import Contract

The importer receives an explicit mode instead of inferring behavior from Profile availability.

- Full mode preserves the current candidate-save behavior.
- Incremental mode requires an eligible replay Profile before PSD extraction starts. It stages the generated candidate, reapplies every stored cleanup stage, and replaces the target only after all stages succeed.
- An ineligible or failed incremental run stops with an error. It must not call the full candidate save path.

## Eligibility

Eligibility is centralized in the cleanup replay Profile API. It validates the source PSD GUID, target Prefab path, target GUID, schema, and nonempty replay-stage list. The Inspector and incremental importer call the same API so button visibility and execution cannot diverge.

## Testing

- Full generation remains available without a Profile.
- Incremental eligibility accepts an exact valid Profile.
- Missing, GUID-mismatched, path-mismatched, and empty-stage Profiles do not expose incremental update.
- Incremental execution rejects an ineligible Profile before the full-save fallback can run.
