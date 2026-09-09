# AGENTS.md — Side Quest

## What this project is

Side Quest is an AI-driven life-sim (see [docs/design-doc.md](docs/design-doc.md) for the full architecture). Unity + C#. The player is one ordinary NPC in a persistent town — the world runs whether they're there or not.

## Where this runs

Code is written/edited here, via Claude Code, in a Codespace with **no Unity Editor attached**. The actual Unity Editor runs on a separate machine; scene/prefab wiring and play-testing happen there, not here.

Don't assume you can open, build, or play-test the Unity project from this environment — you're editing script files, not running the engine. If a task needs Editor verification (scene state, prefab wiring, actual play behavior), say so explicitly rather than claiming it works.

## What Henry owns vs. what's fair game

Henry reviews and decides on these before they're considered final:

- The core NPC data model (design-doc §7) — schema, relationship graph, memory-stream structure.
- The LOD simulation tiers and the rule for when an LLM call happens vs. doesn't (design-doc §5, §6).
- The utility-AI scoring logic that drives NPC decisions (design-doc §8).

Fair game to build fully autonomously: individual UI screens, one-off content, specific job/interior types, asset import glue, non-critical tooling, and anything else not listed above.

## Verification discipline

Carried over from prior projects — this is the important part. A recurring, expensive failure pattern: something looks like it worked, gets reported as done, and turns out to have been broken the whole time, usually because only one suggestive test ever ran, or a parsing bug silently mangled a value. Apply this every time:

- Don't claim something works from one run. Prove it with a control — show the failure case fails *and* the success case succeeds, not just "I ran it once and it looked right."
- Watch for silent-failure parsing, especially around env vars and structured output (this project leans on schema-validated structured output for Director-layer decisions — design-doc §6, §13 — so this risk is not hypothetical here). A greedy regex or an unhandled edge case can produce a plausible-looking wrong value with zero error thrown. If a value "should" be a certain length/shape, check it explicitly rather than trusting it parsed correctly.
- Say "unverified" when it's unverified. A confident-sounding report on an untested code path is worse than an honest "this compiles but I haven't run it" — false confidence costs more time later than admitted uncertainty costs now.
- A result someone can independently check (a real commit, a real test output, a real screenshot) outranks a description of what should have happened.

## Current status

See [docs/design-doc.md](docs/design-doc.md) §11 for the MVP scope. Skills in `.claude/skills/` (`add-npc`, `llm-cost-guardrail`, `emergent-event`) enforce pieces of this design automatically — see their SKILL.md for trigger conditions.
