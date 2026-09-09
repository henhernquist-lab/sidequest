---
name: emergent-event
description: Makes every new Side Quest storyline follow the full emergent-event lifecycle — deterministic trigger, memory-stream fact, NPC-to-NPC propagation with gossip distortion, diegetic player discoverability, and resolution through existing deterministic systems — instead of becoming a scripted quest with generative flavor text bolted on. Use whenever designing or implementing a new storyline, emergent event, quest-like content, or "story template" — triggers on requests like "new storyline," "emergent event," "add a quest," or "story template."
---

# emergent-event

Reference: `docs/design-doc.md` §9 (Emergent-Event Lifecycle) is the source of truth for the five-step pipeline, and §13 risk #3 (emergent events degrading into scripted quests) is exactly the failure mode this skill exists to catch.

## Why this skill exists

Design-doc §13: "the path of least resistance for 'make this storyline work' is to special-case it with bespoke trigger/resolution code, which defeats the point of the architecture." Every new storyline has to be checked against the full lifecycle before it's considered done — not just designed to feel emergent, but actually built out of the same deterministic pieces every other storyline uses.

## The five-step lifecycle every storyline must satisfy (design-doc §9)

1. **Deterministic trigger.** A Tier-0 condition over *existing* state (relationship sentiment crossing a threshold, a need maxed out for N days, a resource crossing a value) — not a scripted "when player reaches X" flag, not a timer, and not an LLM call. If you can't name the plain condition check that fires this event, it isn't ready.
2. **Memory fact.** The trigger writes a structured fact to the memory stream (design-doc §7) — this is the event's canonical record, and everything downstream must read from it rather than re-deriving the trigger.
3. **Propagation with gossip distortion.** At least one other NPC picks up a *derived* fact through ordinary utility-scored social interaction, and the derived fact is allowed to drift (softened, exaggerated, misattributed) in a bounded deterministic way — not rewritten by an LLM, not a flat broadcast to all NPCs.
4. **Diegetic discoverability.** The player can find out through overheard dialogue, environmental state change, a notice, or similar — never a quest marker or auto-journal entry. If a Tier-2 Director pass proposed the event, its structured output should specify which discoverability hooks to wire, but the hooks are ordinary gated dialogue/scene-state systems, not new bespoke UI.
5. **Execution through existing systems.** Resolution happens via schedule, relationship, and inventory systems that already exist. If resolving this storyline requires new one-off code that only this storyline will ever use, that's the signal it's becoming a scripted quest.

## What to do when asked to build a new storyline

1. Before writing any code, state the trigger condition in one sentence, in terms of existing state fields (needs, relationship sentiment, memory facts) — not in terms of narrative beats. If it can't be stated that way, the design isn't ready to implement yet; push back and work out the trigger first.
2. Confirm what memory-fact type/shape gets written at trigger time, and that it's structured (matches the design-doc §7 memory fact shape), not just a free-text blob.
3. Design at least one propagation path and what distortion is plausible for it (what detail could plausibly soften/exaggerate/misattribute as it passes NPC to NPC).
4. Design the discoverability hook as a diegetic, gated check against the memory fact's existence — not a new notification/marker system.
5. Map resolution entirely onto existing systems (schedule changes, relationship-edge changes, inventory/money changes, scene-state flags). If a step needs something schedule/relationship/inventory can't currently express, that's a signal to extend those systems generally rather than hand-writing a one-off for this storyline.
6. Cross-check with `llm-cost-guardrail` if any step involves an LLM call (Tier-2 origination, Tier-1 flavor dialogue) — the trigger itself must never be an LLM decision.

## Red flags to call out immediately

- A "trigger" that's actually just the player reaching a location or talking to an NPC a first time (that's a scripted-quest trigger, not a deterministic-state trigger).
- Resolution logic that lives entirely inside new code specific to this one storyline, touching no shared systems.
- A quest marker, journal auto-entry, or any non-diegetic discoverability path.
- Propagation that copies the fact verbatim to every NPC instead of running through utility-scored, sentiment-gated spread with distortion.
