# Side Quest — Technical Design Doc

Status: living document. This is the source of truth for architecture decisions; `AGENTS.md` and the `.claude/skills/` skills summarize and enforce pieces of this, but this file wins on conflict.

*(2026-09-11: reconciled from the original draft and the fuller `status/context.md` capture into this single document — see `STATUS.md` for the merge record and flagged open questions. `status/context.md` has been deleted; this file is now the only design doc.)*

## 1. Concept

A persistent 3D town where the player is one ordinary resident among 15–100 AI-driven NPCs who each have jobs, homes, routines, relationships, and goals — and who keep living those lives whether the player is watching or not. No quest log. No main story. The "game" is discovering the stories the simulation generates on its own, the way you'd discover gossip in a real small town: by overhearing it, noticing something changed, or getting pulled into it by accident.

The tagline: "A 3D world where you aren't the main character. You're just one person living inside a world that continues to exist without you."

## 2. Core Gameplay Loop

There's no single loop the way a shooter has "find enemy → shoot → reload." Instead there are three nested loops running at different speeds:

- **Moment loop (seconds):** walk around, talk to whoever's nearby, react to what you overhear or see.
- **Day loop (minutes):** go to your job (if you have one), run errands, build or damage relationships, notice what NPCs are up to.
- **Arc loop (days/weeks of playtime):** long-running threads resolve — a friend's business either succeeds or collapses, a relationship you've been building either deepens or falls apart, your own reputation in town shifts based on the pattern of things you've done.

The player's actual verbs are simple on purpose: move, talk, work, buy/sell, build relationship, observe. The complexity lives in the world reacting to those verbs, not in the verb list itself.

## 3. What Makes It Fun (Not Just a Tech Demo)

This is the single biggest risk to design against, so it's worth being explicit about the answer. A simulation is a tech demo. A simulation becomes a game when:

- **The player has something to want.** Even with no quest log, give players default "attractors" — a crush, a rival, a business they wish existed, a mystery (why did that NPC disappear for a week?). The simulation supplies raw material; the player supplies the desire.
- **Discoverability feels earned, not random.** If every player just wanders and eventually bumps into content, it's a walking simulator. Build light signals — an NPC looking upset in public, a "closed" sign on a shop that was open yesterday, two people not making eye contact anymore — that reward paying attention without hand-holding.
- **The world visibly remembers you.** If an NPC treats you identically whether you helped them or ignored them for a month, the simulation feels decorative. Player actions need to leave real, visible residue in relationship values and memory.
- **Time away matters.** Coming back after a break and discovering things changed is a stated goal — protect this hard, because it's rare and it's the strongest hook.

## 4. NPC Architecture

Each NPC is built from layers, cheapest/most deterministic at the bottom, most expensive/generative at the top. Nothing above a layer runs unless the layer below can't handle it:

1. **Body** — position, animation state, pathfinding target. Pure engine-level, no AI at all.
2. **Needs** — hunger, energy, social, hygiene, etc. Simple decay-over-time values, exactly like The Sims' motive system.
3. **Schedule** — a daily routine template (work block, home block, leisure block) modified by needs (if hunger is critical, break schedule to eat).
4. **Personality** — a fixed set of numeric traits (ambition, extroversion, honesty, risk tolerance, etc.) that bias every decision below without needing any generative call — an ambitious NPC just weights "ask for a raise" higher in the utility scoring.
5. **Utility decision layer** — scores available actions given needs + schedule + personality + world state, picks the highest, all pure math (this is where 95% of NPC "thinking" happens, and it's basically free computationally).
6. **Relationship graph** — numeric affinity/trust/familiarity per NPC-pair, updated by interaction outcomes.
7. **Memory stream** — structured facts, not transcripts (see §7 below).
8. **Generative layer (LLM)** — only invoked for: actual dialogue text, periodic memory summarization, and rare "big decision" flavor (§6). This is the only layer that costs real money or latency, and it should be a small fraction of an NPC's total "thinking."

## 5. World Simulation Architecture

Use a Level-of-Detail (LOD) simulation model — the same trick RimWorld/Dwarf Fortress-style sims use to keep hundreds of agents cheap:

- **Active tier** — NPCs within player render distance. Full pathfinding, full animation, full tick rate.
- **Background tier** — NPCs elsewhere in town but "in play." Their schedule/needs/relationships still advance, but abstractly — no pathfinding or physics, just "NPC X is now at work" state jumps on a much slower tick (e.g. every few in-game minutes instead of every frame).
- **Dormant tier** — NPCs the player hasn't been near in a long time. Frozen with a "last simulated" timestamp. When the player re-approaches (or enough real/game time passes), run a fast catch-up pass: resolve their needs/schedule/relationship deltas for the elapsed time in one batch instead of simulating every tick that was missed. This is exactly the mechanism that makes "disappear for a month, come back to a changed life" affordable instead of requiring the game to simulate that NPC in real time the whole while.

A global event bus connects everything — when something notable happens (job loss, breakup, business opening), it posts an event that any interested system (rumor propagation, the Director AI, nearby NPCs' memory streams) can subscribe to, rather than systems polling each other directly.

## 6. AI/LLM Architecture

This is the part worth designing carefully up front, because it's also the part that determines whether you can afford to run this at all.

Three tiers of AI usage, cheapest to rarest:

- **Tier 0 — Zero-LLM (constant, free):** needs decay, scheduling, utility scoring, pathfinding, relationship math. This handles the vast majority of what makes NPCs feel alive minute-to-minute.
- **Tier 1 — Cheap/frequent LLM calls** (small model, short prompts): actual conversation text when the player is talking to someone; occasional gossip/rumor distortion when a fact passes between NPCs. Keep these short, structured, and grounded in retrieved facts (§7) rather than open-ended.
- **Tier 2 — Rare/expensive LLM calls** (bigger model, richer context, maybe once per in-game day or week): a Director layer — feed it a compressed summary of current world state (who's struggling, whose relationships are strained, what businesses exist) and ask it to propose a small number of plausible next developments. The Director doesn't narrate prose into the game live — it outputs a small structured decision ("NPC_042 starts a food cart business, hires NPC_017") that then gets executed through the deterministic economy/relationship systems. This keeps the actually executed story mechanically grounded even though the idea came from a generative model.

Cost control is a first-class design constraint, not an afterthought — structurally similar to Golem HQ: a cheap/deterministic layer doing the bulk of the work, with an expensive orchestrator layer invoked sparingly and under a hard spend cap. Same principle here: put a hard daily token/cost budget on the Director layer and on per-NPC dialogue calls, cache/reuse similar prompts, and default to the smallest capable model for the frequent tier, reserving anything bigger for the rare Director pass.

Worth reading before building this layer: the Stanford "Generative Agents" paper (the "Smallville" simulation) is the closest existing research to exactly this idea — memory streams scored by recency/importance/relevance, periodic reflection, and LLM-driven planning layered over a simpler simulation. It's a genuinely useful blueprint, not just academic curiosity.

### Prior Art & Divergences

**Park et al., "Generative Agents: Interactive Simulacra of Human Behavior"** (Stanford, 2023 — the "Smallville" paper). Primary reference for the memory-stream architecture (§7): structured, importance-scored, retrievable memories as the substrate for agent behavior, and for the idea of a reflection/synthesis layer sitting above raw memory (informs the Tier-2 Director pass, §6). Side Quest diverges from Smallville's approach in one deliberate way: Smallville lets the LLM layer reason continuously and drive behavior directly; Side Quest treats the LLM as a narrow, bounded, replaceable layer on top of a deterministic simulation that already works without it, specifically to control cost and preserve legibility (§3, §6).

## 7. Data Model

**Finalized (2026-09-11).** Two related but distinct models — Karma and Notoriety are player-facing morality/chaos stats, not something every NPC tracks about itself.

```csharp
public class NPC
{
    public string Id;
    public string DisplayName;
    public int Age;
    public string AppearanceRef; // points to a Blockbench model/skin

    public Personality Personality;
    public Needs Needs;
    public Job Job;              // nullable — unemployed NPCs exist
    public string HomeLocationId;
    public List<string> InventoryItemIds = new();
    public int Money;

    public List<ScheduleBlock> Schedule = new();
    public Goals Goals;
    public Dictionary<string, Relationship> Relationships = new(); // keyed by other NPC's Id
    public List<MemoryFact> MemoryStream = new();

    public SimTier CurrentTier;      // Active | Background | Dormant
    public DateTime LastSimulatedAt;
    public string CurrentLocationId;
    public string CurrentActivity;
}

public class Personality
{
    // fixed at creation, 0.0-1.0 each
    public float Ambition;
    public float Extroversion;
    public float Honesty;
    public float RiskTolerance;
    public float Warmth;
    public float Diligence;
}

public class Needs
{
    // 0-100 each, decay over time at different rates
    public float Hunger;
    public float Energy;
    public float Social;
    public float Hygiene;
}

public class Job
{
    public string Title;
    public string WorkplaceId;
    public int Income;
    public float Performance; // 0.0-1.0, drifts based on decisions/events
}

public class ScheduleBlock
{
    public TimeSpan Start;
    public TimeSpan End;
    public string Activity;
    public string LocationId;
}

public class Goals
{
    public List<string> ShortTerm = new();
    public List<string> LongTerm = new();
}

public class Relationship
{
    public float Affinity;      // -1.0 to 1.0
    public float Trust;         // 0.0-1.0
    public float Familiarity;   // 0.0-1.0, grows with interaction count
    public DateTime LastInteractionAt;
    public RelationshipType Type; // Stranger | Acquaintance | Friend | Rival | Family | Romantic
}

public class MemoryFact
{
    public DateTime Timestamp;
    public string EventType;
    public string Description;
    public float ImportanceScore; // 0.0-1.0, drives retrieval priority
    public List<string> InvolvedNpcIds = new();
}

public enum SimTier { Active, Background, Dormant }
```

```csharp
public class PlayerCharacter
{
    public string DisplayName;
    public int Money;
    public List<string> InventoryItemIds = new();

    public float Karma;        // -1.0 (cruel) to 1.0 (kind) — how you treat people
    public float Notoriety;    // 0.0-1.0 — public chaos/crime level, cop-facing
    public float GigRating;    // 0.0-5.0, Uber-style, gates which gigs appear

    public List<string> CompletedGigIds = new();
    public string CurrentLocationId;
}
```

Key decisions baked into this shape:

- `Job` is nullable — unemployed NPCs are a real, playable state, not an edge case.
- `Relationships` keyed by NPC id in a `Dictionary`, not a list — O(1) lookup for "how does X feel about Y," which gets checked constantly (gossip propagation, dialogue).
- `RelationshipType` is a separate enum from the numeric values — "Rival" is a distinct state, not just "very negative affinity."

Keep the memory stream as structured facts with importance scores, not raw conversation logs — retrieve the top N most relevant/recent/important memories when an NPC needs to "think" or talk, rather than feeding an ever-growing transcript into every call. Periodically (once per day, only for NPCs with notable events) run a cheap summarization pass that compresses many small memories into one "reflection" — this is what keeps context small forever instead of growing unboundedly.

Scaffolded as real C# under `Assets/Scripts/Data/` (one class/enum per file) as of 2026-09-11, with a field-completeness self-check in `Assets/Scripts/Verification/NpcDataModelVerification.cs` — see `STATUS.md` for the verification record.

## 8. NPC Decision-Making

Utility AI, not behavior trees or a monolithic state machine — score every candidate action (eat, go to work, socialize, pursue a goal) using needs + personality + context, and pick the highest score, with some randomness/noise so NPCs aren't perfectly optimal robots. Same category of system The Sims uses — worth reading up on "utility AI" and "Sims motive system" specifically.

Big, narratively important decisions (start a business, end a friendship, propose a promotion) are not decided by the LLM directly — they're triggered by deterministic conditions crossing a threshold (e.g., job satisfaction below X for Y days + savings above Z + ambition trait high enough), and the LLM is only consulted to add flavor/plausibility to a decision the math already decided was possible. This keeps the emergent story grounded and prevents the "LLM randomly decides your best friend hates you now" problem.

## 9. Emergent Events

Events are generated the same way real gossip spreads — as facts with a lifecycle, not scripted triggers:

1. Something happens (deterministic trigger fires — a job is lost, a business opens).
2. It's logged as a memory fact for everyone who witnessed it, with an importance score.
3. NPCs occasionally share known facts with people they interact with, weighted by relationship strength and schedule overlap — this is a graph propagation problem, not an LLM call per share.
4. Each share has a small chance of distortion (a short LLM call that rephrases the fact slightly, exaggerated or garbled) — this is genuinely where a small LLM call earns its cost, since "the game literally makes up realistic gossip drift" is a great emergent-feeling feature for cheap.
5. The player discovers all of this exactly like a real person would — overhearing a conversation, noticing a changed schedule, being told directly by someone involved.

## 10. Performance Strategy

- LOD simulation tiers (§5) are the single biggest lever — most NPCs, most of the time, should be running on the cheap abstracted tier, not fully simulated.
- Batch LLM calls where possible (e.g., run the daily reflection/summarization pass for all NPCs who need it as one batched job overnight, not scattered real-time calls).
- Cap NPC count per LOD tier, not just globally — decide up front how many "active" NPCs the active tier can handle at target framerate on target hardware, and don't exceed it regardless of total simulated population.
- Treat the LLM layer as a service with a queue and a budget, not a blocking call in the game loop — dialogue should feel snappy even if a Director pass is quietly running in the background.

## 11. Realistic MVP

Resist the pull toward "50–100 NPCs" for the first playable build — that's a Phase 3 goal, not an MVP goal. A realistic MVP:

- One small town, a handful of buildings (5–8), one reusable interior template.
- 15–20 NPCs, enough for relationships and gossip to feel real without needing serious LOD infrastructure yet (everyone can basically run on the "active" tier at this scale).
- 3–4 job types, a basic economy (income, spending, one shop).
- Needs/schedule/utility AI fully working — this is the deterministic backbone and needs to feel alive on its own, even with zero LLM dialogue yet, before adding generative flavor on top.
- One complete emergent storyline template working end to end (job loss → business attempt → success/failure → relationship ripple) as proof the emergence mechanic actually works, before building more templates.
- Basic LLM dialogue for direct player conversations only — Director layer can wait.

If this MVP is fun to just wander around in for twenty minutes, the concept is proven. If it isn't, no amount of added NPC count or story templates will fix that — go back and fix the core loop first.

## 12. Roadmap: MVP → Impressive Game

- **Phase 0 — Tech spikes** (no "game" yet): prove each pillar in isolation — pathfinding + schedule sim for ~10 dummy NPCs, one working LLM dialogue exchange grounded in a memory stream, one working memory-write/retrieve cycle. Cheap to throw away if something doesn't work.
- **Phase 1 — MVP:** as described in §11.
- **Phase 2 — Depth:** expand to ~40–50 NPCs, add rumor propagation, add 2–3 more emergent storyline templates, add player agency (get a real job, rent/own a home).
- **Phase 3 — Scale:** introduce full LOD tiering (needed once NPC count grows), add the Director AI layer for procedural storyline generation, add relationship depth (romance, family, rivalries), vehicles.
- **Phase 4 — Polish/city scale:** grow toward ~100 NPCs, add local politics/reputation systems, business ownership for the player, richer interiors, long-session persistence, UX polish.

Each phase should be independently shippable/playable — never let the game be in a state where it "only works once everything is done."

## 13. Recommended Engine/Tech Stack

Unity, as the primary recommendation — not because it's flashier than the alternatives, but for two practical reasons:

- Unity uses C#, which is close enough to Java that almost everything learned in BlueJ/Codewars/Robocode transfers directly — classes, interfaces, inheritance, the whole OOP vocabulary carries over with minor syntax differences.
- Because Unity + C# has enormous representation in AI training data, AI-assisted coding (Claude Code, etc.) is noticeably stronger and more reliable for Unity than for less common engines — genuinely relevant given how much of this project gets built with AI help.

Godot 4 is a legitimate alternative to avoid any engine licensing dependency (fully open source, free forever, no strings) — also supports C#. Tradeoff: a smaller (though growing) body of AI training data and community tutorials, meaning AI-assisted coding may need more correction.

For the LLM layer itself: keep it as a separate service the game calls over HTTP (not embedded), so models/providers can be swapped without touching game code — this also makes it trivial to run the Director layer as an offline/batch job rather than in the game process.

## 14. What Henry Should Personally Understand vs. Freely Vibe-Code

Own these yourself, even if AI writes the first draft:

- The core data model (§7) — every other system depends on this shape being right; getting it wrong late is expensive to unwind.
- The LOD tier architecture and the rule for when an LLM call happens vs. doesn't — this is the cost/performance safety net, and without understanding it, a bug where something accidentally calls the LLM every frame is uncatchable.
- The utility AI scoring logic — this is the beating heart of "do NPCs feel alive," and debugging weird NPC behavior requires actually understanding the scoring math, not just reading AI-generated code and hoping.

Fine to vibe-code more freely: individual UI screens, specific dialogue flavor content, one-off job types or building interiors, asset import/pipeline glue code, non-critical tooling.

The test that matters: if a system breaks, could you debug it yourself with an understanding of why it's built that way — or would you be stuck re-prompting an AI blind? Anything in the first list fails that test if you don't understand it.

## 15. Biggest Technical Risks

### Product/Scope Risks

1. **LLM cost/latency exploding as NPC count grows.** Mitigated entirely by the LOD + tiered-AI architecture above — skip this and call an LLM per NPC per tick, and this project is financially and technically dead on arrival at any real scale.
2. **NPCs feeling robotic despite generative dialogue** ("uncanny valley of simulated life"). Mitigated by grounding every LLM output tightly in structured facts (memory, personality, relationships) rather than open-ended generation, and by making sure the deterministic layer alone already feels alive before generative flavor is added on top.
3. **Scope creep killing the project before an MVP ships.** This is the single most common way ambitious solo/indie sims die. The phased roadmap exists specifically to fight this — do not build toward 100 NPCs before 15 NPCs are proven fun.
4. **Performance at scale is a genuinely hard problem** even for professional teams. Mitigated by the LOD model, and by being honest that the MVP's 15–20 NPC count is chosen specifically because it doesn't require solving that problem yet.
5. **Architecture that works in a demo but doesn't survive complexity growth** — the classic vibe-coding trap. Mitigated by owning the data model and core architecture personally (§14) rather than accepting whatever shape an AI happens to generate first.

### Implementation Risks

1. **LLM cost explosion (the #1 risk).** The entire three-tier architecture (§6) exists to prevent this, but the failure mode is insidious: a single piece of code that calls an LLM per-tick, or on background NPCs, or with an unbounded memory-retrieval query, can silently 10–100x the token bill without any single call looking wrong in isolation. This is why `llm-cost-guardrail` exists as an auto-triggering skill rather than a one-time review — every piece of new dialogue/memory/decision code gets checked against the tier rules, not just code reviewed at a point in time.
2. **Structured-output parsing failures.** Tier-2 Director output must be schema-validated, not hopefully parsed — a malformed or partially-matching response that gets loosely parsed can silently write wrong state (e.g. misidentifying which NPCs are involved in an event) with no error thrown. Ties directly to the verification-discipline principle in `AGENTS.md` about silent-failure parsing.
3. **Emergent events degrading into scripted quests.** Without discipline, the path of least resistance for "make this storyline work" is to special-case it with bespoke trigger/resolution code, which defeats the point of the architecture. The `emergent-event` skill exists specifically to catch this before it accumulates.
4. **LOD catch-up producing implausible or jarring results.** If a dormant NPC's batched catch-up resolution produces a state the player finds obviously wrong (e.g. needs that decayed into an impossible combination, or a storyline that resolved "off-screen" in a way that feels like it cheated the player out of participating), the illusion of a persistent world breaks. Needs careful bounding of what's allowed to resolve without the player present.
5. **Data-model incompleteness at NPC creation.** Per §7, a missing field reads as a false/zero default to the utility scorer rather than erroring — meaning a sloppily-created NPC doesn't crash, it just behaves subtly wrong (unmotivated, broke, friendless) in a way that's easy to miss in testing and hard to diagnose later. The `add-npc` skill exists to make this structurally impossible rather than relying on review discipline.

## 16. The Gig App (GigGo)

A pure life-sim risked feeling boring on its own, so the player gets 3 daily quests delivered through an in-game phone app — working name GigGo (DoorDash/TaskRabbit parody). The app's voice stays flat, corporate-cheerful, and completely deadpan about how absurd the actual tasks are:

> "New opportunity near you! 🎉 Client requests: retrieve stolen garden gnome from rival's yard before sunrise. Est. payout: $45. Tap to accept."

How it stays connected to the sim instead of being bolted on:

- Some listings are pulled directly from current world-state (real NPC beef, real job losses, real business openings feeding the emergent-event system in §9) — others are generic wacky templates for daily variety.
- Post-gig reviews carry the personality the app itself deliberately doesn't. After completing a gig, the involved NPC leaves a star rating + review flavored by their own personality trait vector — an honest NPC is blunt, an extroverted one is over-the-top. This surfaces the world's real reaction to you using data the game already tracks.
- Payout ties directly into the existing `Money` field (§7) — this is the player's actual in-fiction income source, not a side toy.
- A separate gig-rating stat (parodying Uber-style driver ratings) gates which gigs even show up — tank it and gigs dry up or worsen; keep it high and rarer/better-paying ones surface.

## 17. GTA-Adjacent Direction

A pure "normal city life" risked being boring to actually play, even if fascinating to build. The fix is tonal contrast, not tonal consistency (see Yakuza's substory system for the reference point that proves this works) — the simulation underneath stays grounded and believable; the chaos lives entirely in the gig/quest layer sitting on top of it.

What "GTA, but nicer-looking" means mechanically:

- Third-person open-world traversal, eventually including vehicles.
- Vehicles as a shared system, not just a player toy — NPCs already have schedules and destinations (§5); let them actually drive between them sometimes, so traffic is part of the living city, not decoration for the player alone.
- A Notoriety system — this reuses the existing reputation/gossip architecture (§9) rather than being a new system; it's just fed by chaos-type events instead of only relationship events, with cops as an actual NPC job type reacting through the same event bus as everything else.
- Art direction: stylized/vibrant characters, not gritty realism. A deliberate identity choice, and a practical one — photorealistic humans are one of the hardest things to pull off with limited solo/small-team resources, while a polished stylized look reads as intentional rather than budget-constrained.

**Scope note:** vehicles + a Notoriety/police-response system are a real, meaningfully sized addition on top of an already ambitious sim — this is exactly the scope-creep risk in §15. The MVP (§11) stays foot-based: the gig app and 3 daily quests, on foot, in the small town. Vehicles and full Notoriety are explicit Phase 2 "depth" additions, not MVP requirements — same philosophy as NPC count: prove it small before scaling the chaos.

## 18. Karma System

Every gig has (at minimum) two resolution paths baked into its template — a clean way and a dirty way — and either path completes the gig, but shifts a running Karma score:

> Garden gnome gig: Clean = knock, explain, ask for it back. Dirty = break in at night and just take it (maybe trash something extra for worse karma).

Karma is a separate axis from Notoriety, on purpose:

- **Notoriety** = how much public chaos/crime you cause (cop-facing).
- **Karma** = how you treat people (NPC/relationship-facing).

These can diverge interestingly — chaotic-but-kind and clean-but-cruel are both valid, different playstyles, which is more interesting than one meter trying to do both jobs.

How it plugs into existing systems:

- The post-gig review system (§16) is the delivery vehicle — a dirty-path completion generates a bitter/scared review; a clean one generates a grateful one. Karma becomes legible through a mechanic that already exists.
- Karma feeds the gossip/reputation graph (§9) exactly like any other event — screw someone over, and NPCs who talk to them start treating you differently before you've even met them.
- At the extremes, Karma can gate which gigs even appear on GigGo — low enough karma surfaces genuinely shady listings (fence stolen goods, intimidate a debtor); high karma surfaces "good samaritan" flavored ones.
- Long-term, extreme Karma in either direction is good Director-AI (§6) fuel for real storylines — a rival vigilante forming because you've been a menace, or a copycat good-samaritan NPC starting their own gig-helping thing because you inspired them.

**Scope note:** MVP keeps this simple — one running Karma number, two resolution paths per gig, a couple of visible reactions (reviews + gig availability). Director-hooked storylines and shop-price reactions are Phase 2 depth, not day-one requirements.

## 19. Known Risks to Revisit Once a Prototype Exists

Flagged in design review, deliberately not to be solved on paper — these need an actual playable build to evaluate honestly:

1. **Gig repetition.** 3 fresh quests a day sounds great on day one, but a small hand-written template pile will feel reskinned fast. Needs a system that generates variety from combinations (verb + location + NPC personality), not a growing pile of one-offs — otherwise this becomes an unsustainable solo content-writing treadmill.
2. **Karma/Notoriety risk being cosmetic.** If bad karma only changes review text but nothing else, players will ignore the system — it needs real mechanical teeth (gigs locking/unlocking, NPCs refusing interaction, price changes), not just flavor.
3. **The slow emergent-sim loop and the fast gig-chaos loop might not reinforce each other.** Risk: players just gig-loop forever and never engage with the slower emergent stuff that makes the game unique. Gigs should sometimes reveal or trigger real emergent storylines, not sit next to them as a separate minigame.
4. **Movement/driving "feel" is easy to underestimate.** A large amount of why GTA is fun has nothing to do with missions — it's that moving around feels good. Easy to treat as "solved" too early in a solo/AI-assisted project; if movement feels stiff, no quest design saves it.
5. **Zero stakes currently means zero tension.** Nothing stops the player from walking away from a bad situation with no cost. Even lightweight friction (a chance of being spotted mid-dirty-gig, a real if small chase response) gives choices actual weight.
6. **World size vs. NPC density mismatch.** Open GTA-style streets with only 15–20 NPCs (the MVP count, §11) will feel like a ghost town compared to what "GTA vibes" primes players to expect. Either keep the map small/dense so it feels alive at MVP population, or don't lean hard on open-road driving until population scales in Phase 2+.

## 20. MVP Town Layout

Six locations, one reusable interior template (§11's requirement):

- **Corner Diner** — the social hub; where overheard gossip naturally happens.
- **General Store** — the shop, drives the basic economy.
- **Auto Shop** — mechanic job; plants the seed for vehicles later.
- **Police Station** — cop exists as a character from day one, ahead of full Notoriety response (Phase 2).
- **Apartment Complex** — one reusable interior template housing most NPCs and the player.
- **Town Square / Park** — outdoor public space; no job here, but where wacky gigs get staged and NPCs cross paths outside work.

## 21. MVP Job List

Four job types, each tied directly to a location above:

- Barista/Cook — Corner Diner
- Shopkeeper — General Store
- Mechanic — Auto Shop
- Cop — Police Station

The remaining majority of the 15–20 NPC population is unemployed/student/retired — fine, since `Job` is nullable in the data model (§7).

## 22. First Emergent Storyline (Concrete Example)

The one complete end-to-end storyline the MVP needs to prove emergence works (§11):

- **Trigger:** an employed NPC's `Job.Performance` drops below ~0.3 for 5+ consecutive days (driven by low `Diligence` + neglected needs) → their employer has an escalating daily chance to fire them, weighted by the employer's own `Honesty`/`Warmth` (a colder boss fires faster).
- **Beat 1 — job loss:** memory fact logged for the NPC and witnesses/close relationships; income stops, needs decay accelerates.
- **Beat 2 — business attempt:** if `Ambition` is high and some savings remain, after a few more days they start a small business — MVP keeps this to one template, a food cart at Town Square (a new lightweight "workplace" tied to that location).
- **Beat 3 — success/failure:** a daily deterministic check (Town Square foot traffic + owner's `Diligence` + a small random factor) grows or shrinks the business's health over time. Success eventually triggers hiring another NPC (a real new `Job`). Failure closes the cart, logs another memory fact, and ripples into their closest Family/Romantic relationship taking an `Affinity` hit from the stress.
- **Discoverability:** overheard Diner/Town Square chatter, a visibly changed schedule, or being told directly if you know them.

The "hired NPC eventually betrays them" continuation from the original pitch is a natural Phase 2 extension of this exact thread, not an MVP requirement.

## 23. Example GigGo Quests (MVP)

Five quests demonstrating the generic/world-pulled split and clean/dirty branching (§16, §18):

1. **"The Great Gnome Heist"** (generic) — retrieve a stolen garden gnome. Clean: ask/pay for it back. Dirty: break in at night (+Notoriety if spotted, −Karma).
2. **"Cart Trouble"** (tied to the Auto Shop) — fetch a part across town fast. Clean: deliver on foot. Dirty: "borrow" a parked car.
3. **"Shift Cover"** (world-pulled) — a Diner barista asks someone to cover their shift because of something real happening in their memory stream that day. Clean: actually work the shift. Dirty: agree, then ditch — the Diner owner's opinion tanks.
4. **"Prime Parking"** (generic) — free up a coveted spot outside the General Store. Clean: politely ask the driver to move. Dirty: slash a tire to force it.
5. **"The Magician's Debut"** (pure wacky) — convince 3 strangers at Town Square you're a traveling magician. Clean: genuine charm/showmanship. Dirty: aggressive lying/intimidation.
