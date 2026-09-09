# Side Quest

An AI-driven life-sim where the player is one ordinary NPC in a persistent town that runs with or without them. See [docs/design-doc.md](docs/design-doc.md) for the full technical design, and [AGENTS.md](AGENTS.md) for how coding agents should work in this repo.

## Two-machine workflow

This project is developed across two machines that share state only through this git repo:

**This machine (Codespace) — code**
- Writing/editing C# scripts (`Assets/Scripts/`), docs, and Claude Code skills.
- No Unity Editor is installed or attached here. Nothing here can build, open a scene, or play-test the game — this environment edits text files, full stop.
- IntelliSense for `.cs` files works here via the .NET SDK + OmniSharp/C# tooling in `.devcontainer/`, without needing the Editor.

**The other machine (Mac) — Unity Editor**
- Opening the project in the Unity Editor, scene/prefab wiring, material/asset setup, play-testing, and builds all happen here.
- This is also where any Unity-generated files (`Library/`, `.csproj`/`.sln`, `Temp/`, etc. — see `.gitignore`) get regenerated locally; none of that is committed.

**The rule that keeps this working: both machines must run the exact same Unity Editor version**, down to the patch number. A version mismatch on either machine will silently rewrite serialized YAML (scenes, prefabs, `ProjectSettings/`) in ways that show up as huge, noisy diffs or outright corrupt merges the next time the other machine opens the project. Check the Editor version in `ProjectSettings/ProjectVersion.txt` once that file exists, and update it deliberately (both machines, same day) rather than letting one machine auto-upgrade.

## Repo layout

```
docs/design-doc.md      full technical design (source of truth for architecture)
AGENTS.md                how coding agents should work in this repo
.claude/skills/          auto-triggering skills (add-npc, llm-cost-guardrail, emergent-event)
Assets/Scripts/          C# source — the only Assets/ content that lives here pre-Editor
.devcontainer/           C#/.NET tooling so this Codespace gets IntelliSense without Unity
```

Everything else under `Assets/` (scenes, prefabs, materials, imported packages) gets created by the Unity Editor on the Mac and committed from there — this Codespace doesn't generate or expect to see it until that happens.

## Merge driver for Unity YAML assets

`.gitattributes` routes `.unity`/`.prefab`/`.asset`/`.mat`/etc. merges through `merge=unityyamlmerge`, but git needs that driver registered locally to actually use it — this only needs to happen on the Editor Mac (this Codespace has no Unity install, so no `UnityYAMLMerge` binary to point at). On the Mac, once Unity's installed:

```
git config merge.unityyamlmerge.name "Unity smart merge"
git config merge.unityyamlmerge.driver "'/path/to/Unity/Editor/Data/Tools/UnityYAMLMerge' merge -p %O %A %B %A"
git config merge.unityyamlmerge.recursive binary
```

The path is inside the Unity Editor install (e.g. on macOS, `/Applications/Unity/Hub/Editor/<version>/Unity.app/Contents/Tools/UnityYAMLMerge`) — swap in the exact path for whichever Editor version you're running.
