# UnityPSDLayoutTool2 Agent Rules

Read `AGENT.md` for repository conventions.

## PSD Hierarchy Cleanup Execution

PSD hierarchy review, planning, naming, grouping, component extraction and verification must run in one primary agent. Do not spawn, resume or delegate to subagents, teams or external advisor agents for any cleanup stage, including read-only review. This task-specific restriction overrides general workspace permission to use subagents.

Read the complete task, skill, plan format and authoritative snapshot once per unchanged version, using bounded non-overlapping chunks if needed. Re-read only changed inputs or a missing/truncated section. After required evidence is collected, write the complete draft JSON and review before preparing validation runners. Use existing helpers; inspect executor source only to resolve a specific documented error or missing capability. Run lint, then simulation, then required Unity preflight and visual checks, and publish the exact validated bytes. Do not weaken approval, preservation or visual gates to save time.

Every five minutes, report elapsed time, actual artifact paths already written, the current blocker and the next concrete action. If the draft is still missing, prioritize completing it or report the specific missing evidence; do not continue speculative tooling or repeated empty-file polling.

## Live GameObject Selection

When a task needs the GameObject currently selected in the Unity Editor, use only NativeUnity. Run `.agents/skills/prefab-hierarchy-cleanup/scripts/read_unity_selection.py --mode selection` against the target Unity project; it calls `unity command eval_file`, where C# reads `UnityEditor.Selection` in the Editor process. Python may launch the command and parse its result.

Do not use uLoop, another third-party MCP/plugin or service, a cached file, a screenshot, or a guessed Prefab path to identify the live selection. Do not fall back to another backend when NativeUnity fails. Report the selection as unverified and include the failure reason.
