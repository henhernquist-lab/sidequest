---
name: llm-cost-guardrail
description: Checks dialogue/memory/decision/Director code against Side Quest's tiered AI architecture and red-flags cost/architecture violations — an LLM call in a per-tick loop, a call on a background or dormant-tier NPC, unbounded memory-stream retrieval, or a significant decision made directly by LLM output instead of by a deterministic trigger the LLM only flavors. Use whenever writing, reviewing, or reasoning about code involving an LLM call, NPC "thinking"/dialogue/decisions, the Director layer, or memory retrieval — triggers proactively any time such code is touched, not just when explicitly asked to review it.
---

# llm-cost-guardrail

Reference: `docs/design-doc.md` §6 (Three-Tier AI/LLM Architecture) and §13 risk #1 (LLM cost explosion — the top technical risk on this project). Re-read §6 if in doubt about which tier a piece of behavior belongs to.

## Why this skill exists

Design-doc §13: "a single piece of code that calls an LLM per-tick, or on background NPCs, or with an unbounded memory-retrieval query, can silently 10–100x the token bill without any single call looking wrong in isolation." This is the #1 technical risk on the project. This skill is the automatic check against it — run it on any code touching LLM calls, not just on request.

## The rule (design-doc §6, non-negotiable)

No Tier-1 or Tier-2 LLM call may:
1. Run inside a per-tick loop.
2. Fire for a background- or dormant-tier NPC (design-doc §5) — LLM calls are Active-tier-only, and only during direct interaction (Tier 1) or on an explicit deterministic trigger (Tier 2).
3. Use unbounded memory-stream retrieval — retrieval must be top-k / explicitly bounded, never "pull the whole stream and let the model sort it out."
4. Be the thing that *decides* a significant outcome. A deterministic trigger condition (design-doc §9 step 1) decides *that* something happens; the LLM may only flavor/phrase it (Tier 1) or, for Tier 2, propose a small structured decision that still executes through deterministic systems (design-doc §9 step 5) — never a free-text decision that gets loosely parsed and acted on directly.

## What to check when reviewing or writing this code

- **Loop context.** Is the call site inside anything that runs every tick, every frame, or on a fixed short timer? If so, that's a Tier violation regardless of what the call does — gate it behind an explicit event/trigger instead.
- **LOD tier of the target NPC(s).** Does the code check `lodTier` before calling, or does it assume Active? Background/dormant NPCs must never reach an LLM call path — including indirectly, e.g. a batch job that loops over "all NPCs" and calls per-NPC without filtering by tier first.
- **Memory retrieval bounds.** Is there an explicit top-k / limit / relevance cutoff on what gets pulled from `memoryStream` before it goes into a prompt? Flag any retrieval that passes the full stream or an unbounded query.
- **Structured output validation.** For Tier-2 Director calls: is the output schema-validated before being executed, or hopefully parsed? A malformed/partial match that's loosely parsed can silently write wrong state with no error thrown — tie this to the verification-discipline principle in `AGENTS.md` about silent-failure parsing.
- **Who decides vs. who flavors.** Trace whether the actual state mutation (goal assignment, relationship change, memory-fact write) happens in deterministic code that received a small structured signal, or whether the LLM's output is itself being treated as the decision. If it's the latter, that's a Tier violation even if the call itself is otherwise well-bounded.
- **Trigger provenance for anything new.** If this is a new emergent event or Director-originated behavior, confirm it traces back to a deterministic trigger condition (see the `emergent-event` skill) rather than being invoked speculatively "to see what the model comes up with."

## How to respond when you find a violation

Name the specific rule violated (1–4 above), point at the exact call site, and propose the deterministic-tier-compliant fix (add an LOD check, bound the retrieval, move the decision into a trigger condition with the LLM call only flavoring the result) rather than just flagging it and moving on. If the violation is intentional/being prototyped in a tech-spike context (design-doc §12 phase 1), say so explicitly and note it needs to be resolved before this code leaves spike status.
