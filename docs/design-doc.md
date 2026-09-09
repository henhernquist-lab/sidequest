# Side Quest — Technical Design Doc

Status: living document. This is the source of truth for architecture decisions; `AGENTS.md` and the `.claude/skills/` skills summarize and enforce pieces of this, but this file wins on conflict.

## 1. Concept

Side Quest is an AI-driven life-sim. The player is not the chosen one — they're one ordinary resident of a small persistent town, alongside 15–20 NPCs who have their own jobs, routines, relationships, and problems. The town's simulation runs whether the player is watching or not: NPCs go to work, form opinions, fall out with each other, and generate their own small dramas. The player's role is to notice what's happening and choose whether, and how, to get involved.

The core promise: no two playthroughs produce the same town history, because the events aren't scripted quests — they're the emergent output of NPCs with needs, personalities, and memories acting on a shared, persistent world state.

## 2. The Three Nested Gameplay Loops

**Moment loop (seconds):** the player walks around, talks to an NPC, does a small physical action (pick up an item, open a door, hand something over). This is the "feel" layer — has to be responsive and immediate, zero dependency on anything slow (no LLM call ever blocks this loop).

**Day loop (minutes):** the player picks a handful of things to do with their day — go to work, run an errand, catch an NPC during their lunch window, follow up on something they overheard. NPCs are running their own day loop in parallel on the same clock. This is where the schedule system and utility-AI decisions live.

**Arc loop (hours to the whole playthrough):** the slow accumulation of relationship states, town-wide facts, and emergent storylines — a feud that started three in-game days ago finally boils over, a rumor the player started comes back distorted, a shopkeeper's slow decline into debt becomes visible only if you've been paying attention. This loop is carried entirely by the memory stream and event propagation system (§9), not by hand-authored quest triggers.

The three loops share one clock and one world state; they differ only in what timescale of consequence they surface to the player.

## 3. What Makes This Fun vs. a Tech Demo

A simulation where NPCs run schedules and have needs is not, by itself, a game — it's a spreadsheet with sprites. "Fun" requires three things the architecture has to actively protect:

- **Legibility.** The player has to be able to form correct guesses about *why* an NPC did something. If the reasoning is invisible or contradicts observable behavior, it reads as broken AI, not emergent AI. This is why the utility-AI layer (§8) exists as a distinct, inspectable layer rather than folding decisions straight into an LLM call — deterministic scoring is debuggable and can be surfaced to the player as flavor text ("Mara's tired and the bar's still open").
- **Discoverability, not delivery.** Emergent events must be findable through diegetic means — overheard conversation, a posted notice, a visibly locked door, a rumor — never a quest marker. If the player can miss it, that's a feature: the town has to feel like it doesn't need them.
- **Consequence that's proportionate and legible.** Small actions (a kind word, a declined favor) should nudge relationship state in ways that show up later, not vanish into a stat the player never sees. The memory stream (§7) is written specifically so consequences can be recalled and referenced, not just accumulated silently.

The single biggest failure mode to design against: NPCs that are technically "alive" (schedules ticking, needs decaying) but produce nothing a player would ever notice or care about. Every system below is justified only if it eventually produces a moment a player would screenshot.

## 4. Layered NPC Architecture

Each NPC is a stack of layers, ordered cheapest/most-deterministic at the bottom to most expensive/most-generative at the top. Every layer above the bottom is optional per-tick — an NPC can be fully simulated using only the bottom layers, and the upper layers only activate when their trigger conditions are met (see LOD, §5, and the AI tiers, §6).

1. **Body** — position, current animation/action state, physical world interactions (pathing, object interaction). Pure Unity/C#, no AI involvement.
2. **Needs** — a small vector of decaying motives (hunger, energy, social, hygiene, fun, money-pressure), modeled directly on The Sims' motive system (§14). Decays on a fixed tick, clamped, no LLM involvement ever.
3. **Schedule** — a data-driven timetable of default activities per time-of-day/day-of-week (work block, lunch block, home block), which sets the *default* action if nothing more urgent wins the utility pass.
4. **Personality** — a small fixed set of trait weights (e.g. sociability, conscientiousness, temper, risk tolerance) that bias the needs decay rates and the utility scoring in layer 5. Set at NPC creation, effectively static.
5. **Utility decisions** — the deterministic scorer (§8) that picks the NPC's actual next action each decision tick by combining needs, schedule default, personality bias, and any pending goals/events. This is the layer that actually chooses behavior; everything below it is inputs, everything above it is context/output.
6. **Relationships** — a per-NPC graph of directed edges to other NPCs/the player, each with a sentiment scalar and a small set of tags (e.g. `friend`, `owes-favor`, `rival`). Updated by the utility layer and by memory-stream writes; read by the utility layer as an input (e.g. won't route a favor request to someone with negative sentiment).
7. **Memory stream** — an append-only log of structured facts (§7) with importance scores, the substrate for everything resembling "reasoning about the past." This is what the LLM layer above reads from and writes to; it's also what deterministic systems query for propagation (§9) without ever needing an LLM.
8. **Generative LLM layer** — the only layer that calls a model. Reads a small, bounded slice of the layers below (current needs/goal, relevant relationship, top-k retrieved memories) and produces either flavor dialogue (cheap tier) or a structured Director decision (expensive tier) — see §6. Never mutates world state directly; its output is always executed through the deterministic systems in layers 2–7.

The ordering is the architectural contract: **information flows up freely, but authority flows down** — the LLM layer can request or suggest, but every actual state mutation happens through deterministic code that would produce a valid (if less flavorful) result even if the LLM layer were stubbed out entirely. This is what keeps a stalled/rate-limited/wrong LLM call from corrupting simulation state.

## 5. World-Simulation LOD Model

15–20 NPCs simulated at full fidelity everywhere, all the time, is affordable. The LOD system exists so the architecture *scales* past MVP without a rewrite, and so it's cheap even at MVP scale to leave a fully idle testing session running.

Three tiers, assigned per-NPC per-frame based on distance from the player/camera and whether the NPC is in a loaded scene area:

- **Active** — NPC is on-screen or in the player's immediate area. Full tick rate: body, needs, schedule, utility decisions, relationship updates all run every simulation tick. This is the only tier where the cheap-dialogue LLM tier (§6) can fire, and only in direct interaction.
- **Background** — NPC is in a loaded but off-screen area (elsewhere in the same town scene). Reduced tick rate: needs/schedule/utility still evaluate, but on a longer interval (e.g. once per in-game 10–15 minutes instead of every tick) and with no animation/pathing detail — just state transitions. No LLM calls of any tier fire here (see the guardrail in §6 and the `llm-cost-guardrail` skill).
- **Dormant** — NPC is not in a loaded area at all (off in an unsimulated part of the world, or the player has been away from them for a long stretch). No per-tick simulation. Instead, the NPC accumulates real-world/in-game elapsed time against their last-known state.

**Catch-up pass:** when a dormant NPC transitions back to background/active (player enters their area, or the game queries them for any reason), a single batched resolution runs: needs are decayed in bulk for the elapsed duration, the schedule system fast-forwards through what block they'd currently be in, and any pending deterministic events they were eligible for are resolved in trigger order — all without a frame-by-frame replay and without any LLM call. The result is an NPC who's plausibly "been living their life" the whole time the player wasn't looking, computed in one batch instead of N ticks. This is the single most load-bearing performance decision in the project: it's what makes "the world runs without you" affordable instead of requiring 20 NPCs ticking at full fidelity forever.

## 6. Three-Tier AI/LLM Architecture

This is the layer that keeps the project financially and architecturally viable. Every piece of NPC "intelligence" is assigned to exactly one of three tiers, and the tier is a property of the *system*, not a runtime judgment call:

**Tier 0 — Zero-LLM deterministic bulk.** Needs decay, schedule execution, utility-AI scoring, relationship-edge updates, memory-stream writes for routine events, LOD catch-up resolution. This tier handles essentially all NPC behavior essentially all the time. No network call, no latency, no cost, fully deterministic and testable.

**Tier 1 — Cheap, frequent dialogue calls.** Small, bounded LLM calls that flavor an already-decided interaction — e.g. the player talks to an NPC whose utility layer has already picked their current goal/mood; the call's job is to phrase a line of dialogue consistent with that state and a handful of retrieved memories, not to decide anything. Strict input bounds (top-k memories, not the whole stream), strict output bounds (dialogue text, not action tokens), only fires for Active-tier NPCs during direct interaction. This tier can fire often because each call is small.

**Tier 2 — Rare, expensive Director-layer passes.** A structured-output call that looks at accumulated memory-stream facts across one or more NPCs and decides *that something should happen* — e.g. "these two NPCs have enough negative-sentiment interactions logged that a public falling-out is now plausible." The output is never prose and never a direct world mutation: it's a small structured decision (an event type + involved NPCs + a couple of parameters) that gets hand off to deterministic systems (schedule injection, relationship-edge changes, memory-fact writes) to actually execute. This tier fires rarely — gated by explicit thresholds on the deterministic trigger conditions in §9, never on a timer — and is the only tier allowed to originate a new emergent storyline.

The rule that must never be violated, and the thing `llm-cost-guardrail` exists to catch automatically: **no tier-1 or tier-2 call may run in a per-tick loop, on a background/dormant-tier NPC, or with unbounded memory retrieval, and no significant decision may be made directly by LLM output without a deterministic trigger condition and deterministic execution path.** The LLM is always a narrow, bounded, replaceable component sitting on top of a system that already works without it.

## 7. NPC Data Model

Every NPC is a single struct/record with **all** of the following fields populated at creation — none are optional, because a missing field doesn't error, it reads as zero/false to the utility-AI scorer and silently biases behavior (e.g. a missing `money` field reads as broke, a missing personality trait reads as maximally passive). This is exactly what the `add-npc` skill exists to guarantee.

```
NPC
├─ id                string, stable unique identifier
├─ name               string, display name
├─ age                int
├─ personalityTraits  small fixed-schema struct (e.g. sociability, conscientiousness,
│                     temper, riskTolerance), each 0.0–1.0
├─ needs              vector of decaying motives (hunger, energy, social, hygiene,
│                     fun, moneyPressure), each 0.0–1.0, with per-need decay rates
├─ job                { title, workplaceId, shiftSchedule }
├─ home               { locationId }
├─ inventory          list of { itemId, quantity }
├─ money              int/decimal
├─ schedule           data-driven timetable, time-of-day/day-of-week → default activity
├─ goals              ordered list of active goals, each with a source (schedule default,
│                     utility-emergent, Director-assigned) and completion condition
├─ relationships      graph: map<otherNpcId, { sentiment: float, tags: set<string> }>
├─ memoryStream       append-only list of structured facts:
│                     { timestamp, type, participants[], summary, importanceScore,
│                       decayedImportance (recomputed, not stored raw) }
└─ lodTier            current simulation tier (active/background/dormant) + last-simulated
                      timestamp, used by the catch-up pass
```

Memory facts are structured, not free text, specifically so Tier-0 systems (propagation, the catch-up pass, relationship updates) can query and act on them without ever invoking an LLM. Free-text summaries exist inside a fact only as an LLM-readable convenience field for Tier 1/2 calls — no deterministic system is allowed to depend on parsing it.

## 8. Utility-AI Decision Making

Each decision tick (rate depends on LOD tier, §5), an NPC's utility layer scores its candidate actions and picks the highest-scoring one. Candidates come from three sources: the schedule's current default block, any active goals (from schedule, emergent Tier-0 triggers, or a Director assignment), and a small set of always-available fallback actions (idle, wander, socialize-with-nearest).

Each candidate action has a scoring function that combines:

- relevant need deltas (how much would this action reduce hunger/energy/social/etc. — modeled after The Sims' motive-satisfaction scoring, §14),
- personality bias (a sociable NPC upweights social actions; a high-temper NPC upweights confrontation options when one's available),
- schedule fit (being at the "supposed to be at work" block strongly upweights work-adjacent actions),
- relationship context (favor requests route toward positive-sentiment relationships; avoidance behavior upweights around negative-sentiment ones),
- goal urgency (a Director-assigned or player-triggered goal can carry an explicit priority weight).

The winner executes deterministically. This layer is intentionally simple, inspectable, and LLM-free — it's the thing that makes NPC behavior debuggable ("why did Mara go to the bar" has a scored, loggable answer) and it's the layer the design doc treats as one of the three pieces Henry reviews personally before it's considered final (the others being the core data model and the LOD/LLM-trigger rule), because a bad scoring function is a wrong-feeling game, not a crash.

## 9. Emergent-Event Lifecycle

This is the pipeline every new storyline must follow — enforced by the `emergent-event` skill — so that "emergent" doesn't quietly degrade into "scripted quest with generative flavor text."

1. **Deterministic trigger.** A Tier-0 condition over existing state crosses a threshold — e.g. two NPCs' mutual sentiment drops below a value after N negative interactions, or an NPC's `moneyPressure` need stays maxed for several in-game days. No LLM involved in detecting this; it's a plain condition check running as part of normal Tier-0 simulation.
2. **Memory fact.** The trigger writes a structured memory fact to the involved NPC(s) — this is the event's canonical record. Everything downstream reads from this fact, not from re-deriving the trigger condition.
3. **NPC-to-NPC propagation with gossip distortion.** Other NPCs who interact with a participant have a chance (utility-scored, based on relationship + social need) to receive a *derived* memory fact referencing the original — and derived facts are allowed to drift from the source (details softened, exaggerated, or misattributed) in a bounded, deterministic way, not via an LLM rewrite. This is what makes rumors feel like rumors instead of a broadcast state update.
4. **Player discoverability.** The event becomes findable through diegetic channels only — overhearing a conversation that references the memory fact, a notice board, a visible behavior change (a shop closed, two NPCs no longer seen together). No quest marker, no journal auto-entry. If a Tier-2 Director pass was involved in originating the event (§6), its structured output includes what discoverability hooks to wire up, but the hooks themselves are ordinary deterministic systems (dialogue lines gated on memory-fact existence, scene-state flags).
5. **Execution through existing systems.** Resolution — the shop actually closes, the NPCs actually stop scheduling time together, the player's choice actually changes an outcome — happens entirely through the systems that already exist for schedule, relationship, and inventory state. A storyline is not allowed to require bespoke one-off code to resolve; if it does, that's a sign it's becoming a scripted quest rather than a configuration of existing systems, and the `emergent-event` skill should flag it.

## 10. Performance Strategy

- **LOD tiers (§5)** are the primary lever: full-fidelity simulation is bounded to whatever's Active, background NPCs run on a coarse interval, dormant NPCs cost nothing until a catch-up pass.
- **Catch-up batching** turns "N ticks of absence" into one O(1)-ish resolution regardless of how long an NPC was dormant, which is what keeps returning to the game after a long absence cheap.
- **Tier-0-first design (§6)**: the default assumption for any new NPC behavior is "can this be done with zero LLM calls," and Tier 1/2 are opt-in additions layered on top of behavior that already works without them — never the only way a behavior can happen.
- **Bounded LLM inputs/outputs**: Tier 1 calls retrieve top-k memories (not the full stream) and produce short bounded dialogue; Tier 2 calls produce small structured output, never prose to be parsed loosely. Both bounds exist specifically to cap latency and cost per call, and to avoid the silent-failure parsing risk called out in `AGENTS.md`'s verification discipline (structured output must be validated against a schema, not hopefully parsed).
- **Explicit trigger gating over polling/timers** for Tier 2: Director passes fire because a deterministic condition was crossed, not on a fixed interval, so cost scales with how eventful the town actually is, not with wall-clock time.

## 11. Realistic MVP

Deliberately small, chosen to prove the *whole pipeline* end to end rather than any one system in isolation:

- **15–20 NPCs**, all data-model-complete (§7), living in **one small town** (a handful of locations: a few homes, one or two workplaces, one social space).
- Full three-loop gameplay (§2) functioning for that population: moment-to-moment movement/interaction, a real day/schedule loop, and enough elapsed time for arc-level consequence to be visible.
- LOD system implemented for all three tiers, exercised for real (the player should be able to leave the town's one social hub, come back, and see a background NPC's catch-up-resolved state).
- **Exactly one complete emergent storyline**, carried through the full lifecycle in §9 — deterministic trigger, memory fact, at least one propagation hop with visible gossip distortion, at least one diegetic discoverability path, and resolution through existing systems, no bespoke quest code. This one storyline is the MVP's real deliverable: if it works end to end, the architecture is proven; if it doesn't, no amount of additional NPCs or polish matters yet.
- Both LLM tiers wired and functioning, but deliberately not stress-tested at scale — MVP validates correctness and cost-per-call, not cost-at-scale (that's a later phase, §12).

## 12. Phased Roadmap

1. **Tech spikes.** De-risk the unknowns before committing architecture: one NPC through all seven layers (§4) including a real Tier-1 dialogue call and a real Tier-2 structured Director call; a minimal LOD transition with a working catch-up pass; a schema-validated structured-output call (to surface silent-parsing failure modes early, per the verification discipline). Throwaway code is fine here; the goal is answering "does this approach work at all," not shipping.
2. **MVP.** Build §11 for real: 15–20 data-complete NPCs, one town, full three-loop gameplay, all three LOD tiers, one complete emergent storyline end to end.
3. **Depth.** More storyline *types* (not just more NPCs) — prove the emergent-event lifecycle generalizes across different trigger conditions (relationship-based, need-based, resource-based) rather than being special-cased for the one MVP storyline. Richer personality/relationship interactions, more Tier-1 dialogue variety.
4. **Scale.** Grow NPC count and town size; this is where LOD and cost-per-call assumptions actually get stress-tested against real numbers instead of MVP-scale approximations. Expect to revisit background-tier tick intervals and Tier-2 trigger thresholds here.
5. **Polish.** Discoverability tuning (are players actually finding events diegetically, or missing everything?), dialogue quality/variety, performance profiling pass, content pass on locations/jobs/schedules.

## 13. Biggest Technical Risks

1. **LLM cost explosion (the #1 risk).** The entire three-tier architecture (§6) exists to prevent this, but the failure mode is insidious: a single piece of code that calls an LLM per-tick, or on background NPCs, or with an unbounded memory-retrieval query, can silently 10–100x the token bill without any single call looking wrong in isolation. This is why `llm-cost-guardrail` exists as an auto-triggering skill rather than a one-time review — every piece of new dialogue/memory/decision code gets checked against the tier rules, not just code reviewed at a point in time.
2. **Structured-output parsing failures.** Tier-2 Director output must be schema-validated, not hopefully parsed — a malformed or partially-matching response that gets loosely parsed can silently write wrong state (e.g. misidentifying which NPCs are involved in an event) with no error thrown. Ties directly to the verification-discipline principle in `AGENTS.md` about silent-failure parsing.
3. **Emergent events degrading into scripted quests.** Without discipline, the path of least resistance for "make this storyline work" is to special-case it with bespoke trigger/resolution code, which defeats the point of the architecture. The `emergent-event` skill exists specifically to catch this before it accumulates.
4. **LOD catch-up producing implausible or jarring results.** If a dormant NPC's batched catch-up resolution produces a state the player finds obviously wrong (e.g. needs that decayed into an impossible combination, or a storyline that resolved "off-screen" in a way that feels like it cheated the player out of participating), the illusion of a persistent world breaks. Needs careful bounding of what's allowed to resolve without the player present.
5. **Data-model incompleteness at NPC creation.** Per §7, a missing field reads as a false/zero default to the utility scorer rather than erroring — meaning a sloppily-created NPC doesn't crash, it just behaves subtly wrong (unmotivated, broke, friendless) in a way that's easy to miss in testing and hard to diagnose later. The `add-npc` skill exists to make this structurally impossible rather than relying on review discipline.

## 14. Prior Art / References

- **Park et al., "Generative Agents: Interactive Simulacra of Human Behavior"** (Stanford, 2023 — the "Smallville" paper). Primary reference for the memory-stream architecture (§7): structured, importance-scored, retrievable memories as the substrate for agent behavior, and for the idea of a reflection/synthesis layer sitting above raw memory (informs the Tier-2 Director pass, §6). Side Quest diverges from Smallville's approach in one deliberate way: Smallville lets the LLM layer reason continuously and drive behavior directly; Side Quest treats the LLM as a narrow, bounded, replaceable layer on top of a deterministic simulation that already works without it, specifically to control cost and preserve legibility (§3, §6).
- **The Sims' motive/utility-AI system.** Primary reference for the needs vector (§7) and the utility-AI scoring approach (§8) — decaying motives driving action selection via a scoring function over candidate behaviors, rather than a behavior tree or state machine. Side Quest's Tier-0 layer is architecturally a modernized version of this system, deliberately kept LLM-free.
