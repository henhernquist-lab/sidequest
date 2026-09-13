---
name: emergent-event
description: Makes every new Side Quest storyline follow the full emergent-event lifecycle — deterministic trigger, memory-stream fact, NPC-to-NPC propagation with gossip distortion, diegetic player discoverability, and resolution through existing deterministic systems — instead of becoming a scripted quest with generative flavor text bolted on. Use whenever designing or implementing a new storyline, emergent event, quest-like content, or "story template" — triggers on requests like "new storyline," "emergent event," "add a quest," or "story template."
---

# emergent-event

Reference: `docs/design-doc.md` §9 (Emergent Events) is the source of truth for the propagation pipeline, and §15 Implementation Risks #3 (emergent events degrading into scripted quests) is exactly the failure mode this skill exists to catch.

## Why this skill exists

Design-doc §15, Implementation Risks #3: "the path of least resistance for 'make this storyline work' is to special-case it with bespoke trigger/resolution code, which defeats the point of the architecture." Every new storyline has to be checked against the full lifecycle before it's considered done — not just designed to feel emergent, but actually built out of the same deterministic pieces every other storyline uses.

## The five-step lifecycle every storyline must satisfy

Steps 1, 2, and 4 below map directly to design-doc §9's numbered list; step 3 combines §9's steps 3 and 4 (propagation and distortion) into one; step 5 (execution through existing systems) isn't a separate numbered step in §9 itself — it's this skill's own operationalization of the anti-scripted-quest principle in §15 Implementation Risks #3, concretely illustrated by the worked example in §22.

1. **Deterministic trigger.** A Tier-0 condition over *existing* state (relationship sentiment crossing a threshold, a need maxed out for N days, a resource crossing a value) — not a scripted "when player reaches X" flag, not a timer, and not an LLM call. If you can't name the plain condition check that fires this event, it isn't ready. (design-doc §9 step 1)
2. **Memory fact.** The trigger writes a structured fact to the memory stream (design-doc §7, the `MemoryFact` class) — this is the event's canonical record, and everything downstream must read from it rather than re-deriving the trigger. (design-doc §9 step 2)
3. **Propagation with gossip distortion.** At least one other NPC picks up a *derived* fact through ordinary utility-scored social interaction (design-doc §9 step 3). Each share has a small chance of distortion (softened, exaggerated, or misattributed) — **this distortion step is itself a bounded, cheap Tier-1 LLM call** (design-doc §6: "occasional gossip/rumor distortion when a fact passes between NPCs" is explicitly Tier 1; §9 step 4: "a short LLM call that rephrases the fact slightly, exaggerated or garbled"), not a flat broadcast to all NPCs and not a free-form rewrite — it stays a small, bounded call flavoring an already-decided propagation, same as any other Tier-1 use. Check any implementation of this step against `llm-cost-guardrail`'s rules (bounded input, Active-tier scoping caveat noted there) same as any other Tier-1 call.
4. **Diegetic discoverability.** The player can find out through overheard dialogue, environmental state change, a notice, or similar — never a quest marker or auto-journal entry. If a Tier-2 Director pass proposed the event, its structured output should specify which discoverability hooks to wire, but the hooks are ordinary gated dialogue/scene-state systems, not new bespoke UI. (design-doc §9 step 5)
5. **Execution through existing systems.** Resolution happens via the schedule, relationship, job, and inventory fields that already exist on `NPC`/`PlayerCharacter` (design-doc §7) — see §22's worked example (job loss → food cart business → success/failure → relationship ripple), which resolves entirely through `Job`, `Ambition`, `Affinity`, and `MemoryFact` writes, no bespoke one-off systems. If resolving a new storyline requires new one-off code that only this storyline will ever use, that's the signal it's becoming a scripted quest.

**Correction from an earlier version of this skill:** this skill previously said gossip distortion happens "in a bounded, deterministic way, not via an LLM rewrite" — that was true of an earlier draft of the design doc, but the current `docs/design-doc.md` (§6 and §9, consistent with each other) explicitly assigns distortion to a Tier-1 LLM call. Fixed here to match the current doc, which is the source of truth per its own header. Flagging this prominently in `STATUS.md` since it changes what this skill should treat as compliant vs. a violation, not just a section number.

## What to do when asked to build a new storyline

1. Before writing any code, state the trigger condition in one sentence, in terms of existing state fields (needs, relationship sentiment, memory facts) — not in terms of narrative beats. If it can't be stated that way, the design isn't ready to implement yet; push back and work out the trigger first.
2. Confirm what memory-fact gets written at trigger time, and that it's a real `MemoryFact` instance (design-doc §7: `Timestamp`, `EventType`, `Description`, `ImportanceScore`, `InvolvedNpcIds`), not just a free-text blob.
3. Design at least one propagation path and what distortion is plausible for it (what detail could plausibly soften/exaggerate/misattribute as it passes NPC to NPC) — and if that distortion is implemented via an LLM call, make sure it's a bounded Tier-1 call per `llm-cost-guardrail`, not an unbounded one.
4. Design the discoverability hook as a diegetic, gated check against the memory fact's existence — not a new notification/marker system.
5. Map resolution entirely onto existing systems (`Schedule`, `Relationships`, `Job`, `InventoryItemIds`/`Money`, scene-state flags). If a step needs something those fields can't currently express, that's a signal to extend those systems generally rather than hand-writing a one-off for this storyline.
6. Cross-check with `llm-cost-guardrail` if any step involves an LLM call (Tier-2 origination, Tier-1 flavor dialogue, or Tier-1 gossip distortion) — the trigger itself must never be an LLM decision.

## Red flags to call out immediately

- A "trigger" that's actually just the player reaching a location or talking to an NPC a first time (that's a scripted-quest trigger, not a deterministic-state trigger).
- Resolution logic that lives entirely inside new code specific to this one storyline, touching no shared systems.
- A quest marker, journal auto-entry, or any non-diegetic discoverability path.
- Propagation that copies the fact verbatim to every NPC instead of running through utility-scored, sentiment-gated spread with distortion.
- An unbounded or free-form LLM rewrite standing in for the distortion step — it must stay a small, bounded Tier-1 call, same discipline as any other Tier-1 use.
