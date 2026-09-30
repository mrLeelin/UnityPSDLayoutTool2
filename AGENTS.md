# UnityPSDLayoutTool2 Agent Rules

Read `AGENT.md` for repository conventions.

## Live GameObject Selection

When a task needs the GameObject currently selected in the Unity Editor, use only NativeUnity. Run `.agents/skills/prefab-hierarchy-cleanup/scripts/read_unity_selection.py --mode selection` against the target Unity project; it calls `unity command eval_file`, where C# reads `UnityEditor.Selection` in the Editor process. Python may launch the command and parse its result.

Do not use uLoop, another third-party MCP/plugin or service, a cached file, a screenshot, or a guessed Prefab path to identify the live selection. Do not fall back to another backend when NativeUnity fails. Report the selection as unverified and include the failure reason.
