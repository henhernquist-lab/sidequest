# STATUS — Side Quest

Session-by-session log. Read this fully before starting work; add a dated entry before finishing. See `AGENTS.md` ("Keeping context current") for the rules on how to use this file. Full raw design-conversation dumps referenced below live in `status/context.md`, not here — keep entries here short.

## 2026-09-10 (design session, not a coding session)

- Full design conversation captured verbatim in `status/context.md` (§1–19), covering: the GigGo gig-app quest system (§16), the GTA-adjacent traversal/Notoriety direction (§17), the Karma system (§18), and 6 known risks flagged to revisit once a prototype exists, not before (§19). Also captured there but not yet in the design doc: recommended engine/tech stack (§13) and what Henry should personally understand vs. freely vibe-code (§14).
- MVP scope is still foot-based, no vehicles/full Notoriety yet — those are Phase 2. Nothing contradicts this session's additions.
- No code written yet as of this entry. Next session's likely first real coding work: NPC data model scaffolding (see the `add-npc` skill) and/or the needs/schedule utility-AI backbone from `docs/design-doc.md` §4/§8.

**Open item, unresolved:** `docs/design-doc.md` does not yet contain the §13–19 content described above — it currently ends at §14 (Prior Art/References), and that §14 is different content from the §14 in `status/context.md`'s numbering. The two documents are on different numbering schemes right now. Someone (Henry) needs to reconcile them: decide what merges into `docs/design-doc.md`, resolve the section-number clash, and confirm nothing in `status/context.md` contradicts what's already written there. Not merging this automatically — flagging it per the rule in `AGENTS.md` instead of silently deciding either document is authoritative.

## 2026-09-11 (design session)

- §7 Data Model **finalized**, rewritten with real C# types — `NPC` and `PlayerCharacter` are two separate classes (Karma/Notoriety/GigRating live on `PlayerCharacter` only, not on every NPC). Henry reviewed and approved this shape. Captured in `status/context.md` §7 (2026-09-11 version, supersedes the 2026-09-10 pseudocode).
- Four new sections added to the `status/context.md` capture: §20 MVP Town Layout (6 locations, 1 reusable interior template), §21 MVP Job List (Barista/Cook, Shopkeeper, Mechanic, Cop — each tied to a location), §22 First Emergent Storyline worked out concretely end-to-end (job loss → food cart business → success/failure → relationship ripple), §23 five example GigGo quests with real clean/dirty paths written out.
- No code written yet in this repo — verified directly (2026-09-11): `Assets/Scripts/` has no `.cs` source files, only a build-tool-generated `AssemblyInfo.cs` under the gitignored local-IntelliSense `obj/` folder. The §7 classes are not yet scaffolded here.

**Correction to this entry, checked against the actual repo state (2026-09-11):** as received from the design conversation, this entry stated the 2026-09-10 open item (whether `docs/design-doc.md` absorbed the repo's original §13/§14 content) was "handled" per Henry. That claim doesn't match this repo: `git log` shows `docs/design-doc.md` has exactly one commit — the initial one — and the working tree is clean, so it has not been edited at all since it was first written. §13–23 have not been merged into it in any form. Leaving the 2026-09-10 open item unresolved rather than accepting "handled" at face value — flagging the contradiction here instead of silently editing the design doc or silently marking the item closed. Whoever said it was handled may be thinking of a different session, a local uncommitted edit that never made it in, or may be mistaken — worth clarifying before the next reconciliation attempt.

Next likely coding step: scaffold the `NPC` and `PlayerCharacter` classes for real (use the `add-npc` skill), then the needs/schedule utility-AI backbone.

## 2026-09-11 (coding session)

- Scaffolded the full data model as real C# under `Assets/Scripts/`: `Data/NPC.cs`, `PlayerCharacter.cs`, `Personality.cs`, `Needs.cs`, `Job.cs`, `ScheduleBlock.cs`, `Goals.cs`, `Relationship.cs`, `RelationshipType.cs`, `MemoryFact.cs`, `SimTier.cs`, one class/enum per file. Also `Verification/NpcDataModelVerification.cs` — factory methods for a fully-populated `NPC`/`PlayerCharacter`, a deliberately incomplete `NPC` (negative control), and a reflection-based field-completeness checker.
- **Verified, not just compiled:** `dotnet build` against the existing `Assets/Scripts/SideQuest.Scripts.csproj` succeeded (0 warnings, 0 errors) — real output, not assumed. Separately ran the actual verification logic (not just checked it compiles) via a throwaway runner in the scratchpad directory that links the real source files: fully-populated `NPC`/`PlayerCharacter` pass the field-completeness check, the deliberately incomplete `NPC` is correctly caught with the exact missing fields listed (`AppearanceRef`, `Personality`, `Needs`, `Job`, `HomeLocationId`, `Goals`, `CurrentLocationId`, `CurrentActivity`), and explicit value-field spot checks pass. This satisfies the control requirement in `AGENTS.md` — failure case fails, success case succeeds, not a single suggestive run.

**Judgment calls made — flagging per "Henry reviews the core data model":**

1. **Biggest one: built against `status/context.md` §7, not `docs/design-doc.md` §7.** The task said "exactly matching design-doc.md §7," but that file still has the old pseudocode schema (`personalityTraits`, a 6-field needs vector with `fun`/`moneyPressure`, no `PlayerCharacter` class, `lodTier` instead of `CurrentTier`/`LastSimulatedAt`). The field/type names actually requested (`Personality`, `Needs`, `ScheduleBlock`, `PlayerCharacter` with `Karma`/`Notoriety`/`GigRating`, etc.) match `status/context.md` §7 exactly, so that's what got built. `docs/design-doc.md` itself is still unmodified — this is the same reconciliation gap noted in the 2026-09-10 and 2026-09-11 entries above, now also blocking a literal reading of "match design-doc.md."
2. **`Needs` dropped two fields going from the old schema to the finalized one.** Original `docs/design-doc.md` pseudocode had 6 needs (`hunger, energy, social, hygiene, fun, moneyPressure`); the finalized C# version in `status/context.md` has only 4 (`Hunger, Energy, Social, Hygiene`) — `Fun` and `MoneyPressure` are gone. Built to match the finalized version as instructed, but this is a real behavior change (two fewer utility-AI inputs) worth confirming was intentional, not dropped by accident during the rewrite.
3. **Namespace `SideQuest.Data` / `SideQuest.Data.Verification`** — neither source specifies one; picked something reasonable, trivial to rename.
4. **`RelationshipType` enum** was only given as inline prose in `status/context.md` ("Stranger | Acquaintance | Friend | Rival | Family | Romantic"), not as a code block — wrote it as a 6-value enum in that order. Small judgment call since only prose was given.
5. **Verification checker's real limitation:** reflection can tell a null reference field (string/class/List/Dictionary) apart from a populated one, but it cannot tell "a float explicitly set to 0" from "a float nobody ever set" — both are indistinguishable `0f` in C#. Handled by spot-checking a handful of value-type fields against their exact expected values rather than claiming exhaustive coverage. For value types, the `add-npc` skill's "diff field-by-field out loud" discipline is still the real safeguard, not this tool.

Next likely step: needs/schedule utility-AI backbone (design-doc §4/§8), and resolving the `docs/design-doc.md` reconciliation gap (item 1 above) before it blocks more work referencing "design-doc.md §N" by name.

## 2026-09-11 (reconciliation session)

**The reconciliation gap flagged in every prior entry is now resolved.** `docs/design-doc.md` and `status/context.md` merged into one final `docs/design-doc.md` (23 sections, matching `status/context.md`'s numbering exactly — no other section was added or removed, so no other cross-reference in the document needed renumbering). `status/context.md` deleted — it's now fully absorbed and keeping both around would just reintroduce the drift that caused this whole reconciliation problem.

What moved, verified line-by-line before deleting the source:

- §6 (AI/LLM Architecture) gained explicit **Tier 0 / Tier 1 / Tier 2** labels on its three bullets, plus a new "Prior Art & Divergences" subsection containing the Smallville-divergence paragraph from the old doc's §14, verbatim.
- §15 (Biggest Technical Risks) split into two labeled subsections: "Product/Scope Risks" (the old `status/context.md` §15's 5 items, unchanged) and "Implementation Risks" (the old `docs/design-doc.md` §13's 5 items, verbatim, each still pointing at its matching skill).
- Verified after writing: 23 top-level sections present, both risk subsections present, the Smallville paragraph present verbatim, and every `Tier-0/1/2` reference in the imported risks (`Tier-2 Director output`, etc.) now resolves to an actual definition in §6 — checked with `grep`, not just assumed.

**One addition beyond the two pieces named, not optional:** the "Implementation Risks" import references "Tier-2 Director output" verbatim, but `status/context.md`'s AI/LLM section never actually named Tier 0/1/2 (it described the same three tiers in unlabeled prose). Importing the risks without labeling the tiers would have left a dangling term with no definition anywhere in the merged document. Added the labels to `status/context.md`'s existing three bullets (didn't rewrite the prose) rather than reverting to the old doc's more clinical §6 — flagging since it's the one place this merge did more than what was literally asked.

**Additional inconsistencies found during verification, left alone (not part of the requested merge, flagging instead of silently fixing or silently ignoring):**

1. §4 (NPC Architecture) describes Needs as "hunger, energy, social, money, hygiene, etc." — but the finalized §7 `Needs` class has only `Hunger, Energy, Social, Hygiene` (no money-related field at all). This predates the merge — it was already an internal inconsistency inside `status/context.md` itself — and ties into the still-open question from the 2026-09-11 coding-session entry above about whether dropping `Fun`/`MoneyPressure` from the original 6-need schema was intentional.
2. §16 (GigGo) says "Payout ties directly into the existing `money` field (§7)" — the actual field is `Money` (PascalCase, on both `NPC` and `PlayerCharacter`). Trivial casing nit, also predates the merge, left as-is since it wasn't part of the requested structural change.

**Bigger thing to flag: the three skills reference the pre-merge document and are now stale.** Not touched this session (wasn't asked, and it's a distinct piece of work), but real:

- `add-npc`'s SKILL.md describes the *old* §7 field list (`id`, `name`, `personalityTraits`, a needs vector with `fun`/`moneyPressure`, `lodTier`, etc.) — none of which match the actual scaffolded classes in `Assets/Scripts/Data/`. It also never mentions `PlayerCharacter` at all.
- `llm-cost-guardrail` and `emergent-event` cite specific old section numbers (§5, §6, §7, §9, §13) by name in their SKILL.md bodies — some still resolve correctly by coincidence (§6, §7, §9 happen to cover the same topics in the new numbering), but this wasn't verified section-by-section for every citation, and §13 specifically now means something different (old §13 "Biggest Technical Risks" is now split across §15's two subsections).

Recommend a follow-up pass updating all three skills to match the current data model and section numbers — didn't do it here since it wasn't requested and is a meaningfully separate task from reconciling the two docs.

Next likely step: needs/schedule utility-AI backbone (design-doc §4/§8) is now unblocked since "design-doc.md §N" references are unambiguous again; separately, consider the skill-staleness follow-up above before it causes an auto-triggered skill to give advice against the wrong schema.

## 2026-09-11 (skill-sync session)

Two mechanical fixes to `docs/design-doc.md`, no design changes: §4's needs description no longer lists "money" (the finalized `Needs` class has no money field — money is tracked separately via `NPC.Money`); §16's field reference fixed from lowercase `money` to the real field name `Money`.

Updated all three skills in `.claude/skills/` to match the finalized §7 data model and the reconciled 23-section doc. Verified every section-reference fix by actually reading the target section first (not just renumbering and assuming) — caught two things that were more than cosmetic:

1. **`llm-cost-guardrail` cited "(design-doc §12 phase 1)" for tech-spike context.** Current §12 numbers phases 0–4, not 1–5 — "Phase 0" is tech spikes, "Phase 1" is MVP. Left uncorrected, this citation would have pointed a future reader at the MVP phase instead of the tech-spike phase. Fixed to "§12, Phase 0."
2. **`emergent-event`'s gossip-distortion step directly contradicted the current design doc.** The skill said distortion happens "in a bounded, deterministic way, not via an LLM rewrite" — true of an earlier design-doc draft, but the current `docs/design-doc.md` §6 (Tier 1 bullet) and §9 (step 4) both explicitly say distortion **is** a bounded Tier-1 LLM call, and they agree with each other (not a stray typo in one place). This is a real behavioral change to the skill, not a citation fix: previously it would have flagged any LLM call used for gossip distortion as a violation; now it correctly treats a *bounded* one as compliant, same discipline as any other Tier-1 call. Fixed to match the current doc (source of truth per its own header), with the correction called out explicitly inside the skill file itself, not just here.

Also fixed in `llm-cost-guardrail` and `emergent-event`: stale field-name references (`lodTier`→`CurrentTier`, `memoryStream`→`MemoryStream`) and stale section titles (`"Three-Tier AI/LLM Architecture"`→`"AI/LLM Architecture"`, `"Emergent-Event Lifecycle"`→`"Emergent Events"`). `§13 risk #N"` citations moved to `"§15 Implementation Risks #N"` in both skills — necessary since §15 now has *two* numbered risk lists (Product/Scope and Implementation) with similarly-themed #1 items, so a bare "risk #1" is now ambiguous and every citation needs to name which subsection.

`add-npc` rewritten from the ground up against the actual `NPC` class (field names, `Job`-is-nullable handling, no money-in-needs) and now has a dedicated `PlayerCharacter` section it previously lacked entirely — explicit that `Karma`/`Notoriety`/`GigRating` are player-only and must never end up on `NPC`.

**Flagging for Henry, not resolved, found while reading `llm-cost-guardrail`'s rule 2 closely (not something this task asked me to check, but it fell out of fixing the field names):** §6 assigns gossip/rumor distortion to Tier 1, but §5/§6's "LLM calls are Active-tier-only" rule seems to assume Tier 1 always means "player is directly interacting with this NPC" — gossip distortion is NPC-to-NPC and could plausibly happen between two Background-tier NPCs the player isn't near. Whether that case is exempt from "Active-tier-only" isn't specified anywhere. Left as an explicit open question inside `llm-cost-guardrail` rather than guessing either way — this is a real design decision, not something to sync mechanically.

Next likely step: needs/schedule utility-AI backbone (design-doc §4/§8) — all docs and skills are now internally consistent with each other and with the scaffolded code, so this is unblocked. Separately, Henry may want to weigh in on the two flagged items above (whether the gossip-distortion correction is what he intended, and the Active-tier-only-vs-NPC-to-NPC-gossip open question) before more code gets built on top of them.

## 2026-09-13 (coding session: clock, locations, needs + utility AI)

Built under `Assets/Scripts/`, all plain C# (no UnityEngine references):
- `Core/` — `GameTime` (day + time of day), `GameClock` (real-time ratio, `DayStarted`/`Ticked` events), `SimulationTuning` (**every tunable number, including the clock ratio, lives in this one file**).
- `World/` — `OpeningHours`, `Location`, `LocationRegistry`, `MvpTownData` (the 6 §20 locations as a data table).
- `Simulation/` — `NeedsSystem`, `ResponseCurve`, `ScheduleService`, `FeasibilityEvaluator`, `UtilityScorer`, `ActionScore`, `NpcDecision`, `NpcSimulator` (LOD-aware stepping + dormant catch-up), `NpcAction`.
- `Verification/SimulationVerification.cs` — 10 checks, each behavioral claim paired with a control.

**Verified, run for real (not just compiled):** `dotnet build` on `SideQuest.Scripts.csproj` is 0 warnings / 0 errors. The suite ran via a scratchpad runner linking the real source files: all 10 sections plus the data-model regression check pass, 0 FAIL lines, exit 0. Covered: clock rollover both sides of midnight, Diner open/closed, starving NPC abandons shift (300/300) vs Hunger-80 control stays (300/300), extrovert 100% Socialize vs introvert 100% PursueGoal with an identical-personality control within 0.1 points, feasibility-zero never picked (0/500, final score exactly 0) with feasible controls picked (500/500), LOD tiers (Background/Dormant make zero scoring draws), typo'd schedule activity caught, scheduled venue honored, and a 24h timeline for one NPC.

**Found by reading the timeline, then fixed:** the Active path ignored `ScheduleBlock.LocationId`. During her 19–22 Leisure block, Mara socialized at the Corner Diner, the alphabetically first open venue, instead of the Town Square her block names, while the Background path did use the block's location, so the two tiers disagreed about where she was. Venue preference is now scheduled location → current location → home → first open public venue. Added check 8 with a control.

**Judgment calls for review:**

*Clock*
1. Ratio is 12 game-minutes per real second, so one in-game day takes 2 real minutes. Placeholder.
2. No general event bus yet, just plain C# events on `GameClock`. §5 calls for a bus, but nothing subscribes yet.
3. `DayStarted` fires once per day boundary crossed, before `Ticked`, so tick handlers see daily resets already applied.
4. §7 stores `LastSimulatedAt` as `DateTime`; the clock uses `GameTime`. Bridged via `GameTime.GameEpoch` (Day 1 = 2026-01-01) instead of changing the reviewed schema. Unifying them would be a §7 change.

*Locations*
5. Opening hours are my invention (§20 has none): Diner 06–22, Store 08–20, Auto Shop 08–18, Police 24h, Apartment Complex and Town Square always open.
6. Added capability flags §20 doesn't have: `ServesFood` (Diner, Store, Apartment), `IsSocialVenue` (Diner, Town Square), `IsResidence` (Apartment).
7. "Data-driven" here means one C# data table, not a JSON/ScriptableObject asset (there's no Editor here to author one). A 7th location is one new entry.
8. Home is the whole building, `loc_apartment_complex`, with no per-unit ids. The earlier data-model example's `loc_apartment_complex_3b` won't resolve in the registry.
9. Unknown location ids read as closed instead of throwing.

*Needs + scoring (§8, Henry owns)*
10. Rates were hand-tuned by walking one barista day before the first run, not fitted to output. Decay per hour: Hunger 7, Energy 3.5, Social 2.5, Hygiene 3. Restore per hour while doing the action: Eat 55, Sleep 12, Socialize 30, Bathe 60. Energy started at 5/h, which would have put her to bed around 17:00.
11. Urgency curve is `((100 − need)/100)^3`: need 70 → 0.027, need 15 → 0.614.
12. Personality weight is `0.5 + trait` (range 0.5–1.5), so personality modulates a score but can't erase it. Eat/Sleep/Bathe/Idle get a neutral 1.0 because no trait drives them.
13. Noise is multiplicative ±8%, not additive. This is required, not a style choice: additive noise would give a feasibility-zero action a nonzero score.
14. **Schedule urgency is `max(need urgency, 0.35 baseline)` for the scheduled action, with no separate critical-threshold gate.** The "critical threshold" is wherever a need's curve crosses the baseline. For a Diligence 0.6 barista that's Hunger ≈ 27 noise-free; with noise it's a band (Hunger 25 → 91% eat, 27 → 57%, 29 → 15%).
15. **PursueGoal has an invented flat drive (0.12 × Ambition weight)** because goals aren't mechanical yet. Side effect: any NPC with a goal pursues it instead of idling whenever nothing is pressing, so **Idle is effectively only reachable for goal-less NPCs.**
16. Activity strings map as Work→WorkShift, Sleep, Leisure→Socialize, Eat, Bathe, Goal→PursueGoal, Idle. `ScheduleBlock.Activity` is free-form in §7, so a typo silently drops the schedule baseline. `ScheduleService.FindUnmappedActivities` catches it and is verified, but nothing calls it automatically yet.
17. **"Too far" feasibility is not implemented, and can't be against §7:** NPCs have a `CurrentLocationId` but no position. Feasibility is binary (open, shift time, has job/home/goals).
18. WorkShift's location comes from `Job.WorkplaceId`, not the Work block's `LocationId`. If they disagree, the job wins silently.
19. The last venue fallback, when no scheduled, current, or home venue works, picks the first open venue by id, which is arbitrary (no distance data).
20. The day sim decides once per in-game hour, on the state at the start of that hour. Real runtime decision cadence is undecided.

*LOD (§5, Henry owns)*
21. **Off-screen subsistence floor:** Background steps and dormant catch-up clamp every need at ≥ 30, standing in for "they looked after themselves." **It can raise a need, not just stop decay:** an NPC who leaves Active at Hunger 10 comes back at 30. Nothing else restores needs off-screen.
22. Background sets `CurrentActivity`/`CurrentLocationId` from the schedule block at the end of the step, or Idle when there's no block. Dormant `Step` is a pure no-op; all resolution happens in `CatchUp`.

**Observed in the day timeline, flagged, not tuned:**
- **17:00–18:00 nap.** Energy is 38 after her 16:00 bath and nothing is scheduled until 19:00, so Sleep (0.238) beats PursueGoal (0.115). Plausible once; it would read as robotic if she naps at 17:00 every day. Raising the goal drive or adding an evening block would fix it, which makes it a tuning call.
- Meals at 06:00, 14:00, 22:00 fall straight out of 7/h hunger decay against the ≈ 27 crossover. The 14:00 lunch cost one work hour (7 of 8), and the 06:00 breakfast was noise-decided (0.352 vs scheduled Sleep 0.323).

**Not verified:**
- Nothing ran in Unity. Nothing drives `GameClock` from `Update()`, and there's no MonoBehaviour wiring.
- The timeline is one NPC, one day, one seed. No multi-NPC or multi-day run.

Also noticed: `AGENTS.md` still points at `status/context.md` in two places, and that file was deleted in the reconciliation. Stale; not fixed this session.

## 2026-09-13 (verification session)

The 2026-09-11 skill-sync entry's claims were independently re-verified claim-by-claim against actual file contents (not taken on faith, per the verification discipline) — **all confirmed, zero edits needed this session**. What was checked:

- **design-doc §4/§16 fixes:** §4's needs list reads "hunger, energy, social, hygiene, etc." — no "money." Case-insensitive sweep for `money|Money` over the whole doc returns only `NPC.Money` (§7), `PlayerCharacter.Money` (§7), and §16's `Money` — no lowercase `money` reference remains anywhere.
- **add-npc field list vs. actual classes:** all 18 `NPC` fields and all 8 `PlayerCharacter` fields from `Assets/Scripts/Data/` are present and correctly described (Needs' four fields with the no-money note, six Personality traits, nullable-Job handling, all embedded sub-field lists). The §7 quote in "What NOT to do" matches the design doc verbatim. Its only other section refs (§7, §8) resolve correctly.
- **llm-cost-guardrail section refs:** every citation read against its target — §5 (defines Active/Background/Dormant), §6 (three tiers; both quoted passages verbatim), §7 (`CurrentTier`/`SimTier`, `MemoryStream` as `List<MemoryFact>`), §9 step 1 (deterministic trigger), §12 Phase 0 (tech spikes — confirming the earlier phase-numbering fix), §15 Implementation Risks #1 (quote verbatim). The Active-tier-only-vs-NPC-to-NPC-gossip open question is still genuinely unresolved in the doc — correctly left flagged, not resolved.
- **emergent-event section refs:** §9 confirmed to have exactly 5 numbered steps matching the skill's mapping (skill 1↔9.1, 2↔9.2, 3↔9.3+9.4 combined, 4↔9.5, 5 not a §9 step); §6 Tier-1 distortion quote and §9 step-4 quote both verbatim; §15 Implementation Risks #3 quote verbatim; §22 is the food-cart worked example as claimed and resolves through `Job`/`Ambition`/`Affinity`/`MemoryFact` as the skill says; §7 `MemoryFact` fields all correct.

Nothing was changed in the design doc or any skill this session — this entry exists so the next session knows the skill-sync work has been independently verified, not just claimed in its own entry.

**Still uncommitted:** the entire skill-sync/reconciliation change set (design-doc merge, deleted `status/context.md`, all three skill updates, and the `Assets/Scripts/` scaffolding) is working-tree-only — the repo still has just 2 commits. Commit when ready.

Henry's two open items from the 2026-09-11 skill-sync entry (gossip-distortion behavioral change intent; Active-tier-only vs NPC-to-NPC gossip) remain open — unchanged by this session.

## 2026-09-13 (5-day repeat check, no code changes)

- Ran the check-9 barista (same setup, seed 42) for 5 days back to back with no reset, as a throwaway program in the scratchpad. The repo is untouched. Control: the run's Day 1 is byte-identical to check 9's timeline.
- **The 17:00–18:00 nap and the 22:00 dinner flagged in the coding-session entry are not repeating patterns.** The nap happened on Day 1 only; the 22:00 dinner on Days 1–2 only.
- Energy at 17:00 was 38 on Day 1 and 58–62 on Days 2–5, which points to the nap coming from the test's low starting Energy (25 at midnight). That's read from the scores, not tested with a different start state.
- From Day 3 on, dinner moves to 17:00–18:00: in the unscheduled afternoon, a half-empty Hunger (~45–50) narrowly outscores PursueGoal.
- Also seen, not investigated: meal times drift earlier across the 5 days (breakfast 06→04, lunch 14→12), and she left her shift to bathe on Day 2 (13:00) and Day 3 (11:00). Still a single seed.

## 2026-09-13 (roster session)

Tasks 1 and 2 from this session's prompt (§4/§16 doc fixes, three-skill sync) were already done in the working tree and were re-verified from live file contents before being accepted — see the verification entry above; no drift found. New work this session: **Task 3, the MVP NPC roster.**

- Created `Assets/Data/npc_roster.json` (folder is new) — 18 hand-authored NPCs, JSON field-for-field mirror of finalized §7 `NPC` (camelCase). 4 hold the §21 jobs one each (Ana/Barista-Diner, Rosa/Shopkeeper-Store, Sal/Mechanic-Auto, Frank/Cop-Police); 14 unemployed/student/retired with `job: null`. No C# written; `Assets/Scripts/` untouched. `Assets/Data/README.md` documents conventions (including: top-level `$`-prefixed keys are documentation, loaders must ignore them; `item_*` inventory ids are forward-declared pending an item registry; schedule times HH:MM 24h, `end < start` wraps midnight).
- Tension web per the request: **2 Rival pairs** (Rosa↔Dante unpaid seed money; Lenny↔Vik noise feud), **5 Family pairs** (Maribel↔Dante, Maribel↔Ruby, Priya↔Rosa, Frank↔Iris, Petra↔Vik), **1 mutual Romantic pair** (Ana↔Jin), plus a deliberate one-sided crush (Ruby→Vik) expressed as asymmetric affinity with `Acquaintance` type on both sides — per README convention, one-sided *feelings* ride on affinity, not on asymmetric `type`.
- §22 seed: **Rosa** — `Job.Performance` 0.27 already below the ~0.3 firing threshold with Diligence 0.25, so the deterministic trigger fires day one when built. Errol is a diligence-collision character (0.2) but unemployed, so he is *not* an employed-trigger candidate — noted explicitly in the JSON `$notes` so nobody counts him as one. Greta is the gossip hub (low honesty, high extroversion) whose retellings of the Rosa–Dante fight differ by retelling — a human face for §9 distortion.
- **Verified with controls, not just written:** a throwaway validator (deleted after use, not committed) machine-checked exact field sets against §7 per NPC, all ranges, every referenced id/location resolvable in-file, schedule overlap + single-primary-sleep-block, Rival/Family/Romantic reciprocity, §21 job counts, §22 seed presence. Clean pass on the real file; then 6 bugs injected into a copy (missing trait, needs out of range, job workplace mismatch, broken Rival reciprocity, overlapping schedule, extra `fun` needs field — the old 6-need schema creeping back) were **all 6 caught**. One grep "MISSED" line during that check was an escaping artifact in the grep pattern, resolved by direct count — the underlying checks fire.
- Honest disclosure: two validator bugs surfaced during development (a pair-counting logic error and an over-strict sleep-block rule that miscounted legitimate nap+sleep schedules) — both caught because validator output was cross-checked against what the data actually contained rather than trusted. Same discipline caught a truncated mid-file edit that briefly spliced two NPC objects together. JSON now parses clean and passes all checks.
- Judgment calls (content-level, fair game per AGENTS.md, but flagging): 9 NPCs authored `Active` / 9 `Background` at authoring time (no `Dormant` — nobody has been away yet); all homes are `loc_apartment_complex` since §20 names it as housing "most NPCs and the player"; memories/timestamps are authored as a "day 0" snapshot (2026-09-12) loaders may re-simulate from.
- **Follow-up worth doing when a loader exists:** the validator was throwaway; a permanent validation pass (or schema check) should live with the loading code and fail loudly on unknown fields/ids/locations — a silently-defaulted NPC is §15 Implementation Risk #5.

Still uncommitted: everything from the reconciliation/skill-sync/verification sessions plus this roster — repo remains at 2 commits.

## 2026-09-13 (roster loader session)

Built `Assets/Scripts/Loading/`: `RosterLoader` (JSON → §7 `NPC`/`PlayerCharacter`), `Roster`, and `RosterError`/`RosterLoadException`. Checks live in `Verification/RosterLoaderVerification.cs` (R1–R6).

**How it fails:** every problem in the file is collected with its path and line/column. If there's even one, `Load` throws `RosterLoadException` and returns nothing, so a half-built NPC never escapes. It catches unknown fields, missing required fields, unknown location ids, relationship and `involvedNpcIds` targets not in the roster, wrong types, and out-of-range values.

**Verified, run for real:** `dotnet build` on `SideQuest.Scripts.csproj` gives 0 warnings / 0 errors.
- **R1, real roster:** 18 NPCs load. Exactly the 4 §21 job holders are employed, with every job field matching the raw JSON; the other 14 have an explicit `job: null`. Rosa's Performance 0.27 and Diligence 0.25 match the file.
- **R2, corrupted copies**, each against a control where the clean file loads: a bad location id, a relationship to a nonexistent NPC, a missing `diligence`, and an unknown `fun` need each produce exactly 1 error at the exact path. The first three combined produce exactly 3.
- **R3, silent-failure traps**, each shown next to the naive behavior it avoids: duplicate key, `"0"` for an enum, `NaN`, `"08:30\n"`, a 12:00–12:00 block, trailing content.
- **R4, player section:** a valid player loads intact; three corruptions give exactly 3 errors.
- **Check 9** is byte-identical after being refactored into the shared `RunDayTimeline` helper.
- **R6, Rosa's day: FAIL, see below.** The suite's overall result is FAIL; I'm leaving it red on purpose.

**Main finding: no employed NPC can ever work.**
- Only 18 of the roster's 134 schedule blocks map to an `NpcAction`, and all 18 are `"Sleep"`.
- Work blocks use narrative labels (`"Counter shift"`, `"Morning repairs"`, `"Day patrol — rounds"`), but `ScheduleService` only recognizes Work/Sleep/Leisure/Eat/Bathe/Goal/Idle. An unmapped block gives no schedule baseline and leaves WorkShift infeasible.
- Rosa's loaded day: Sleep 12h, PursueGoal 6h, Eat 3h, Socialize 2h, Bathe 1h, WorkShift 0h. She spends the afternoon at the Corner Diner and never goes to the General Store.
- The loader is right to accept these labels, since §7 types `Activity` as a free-form string, but it's a real gap between the roster content and the AI.
- **Needs a decision (§7 is Henry's):**
  - (a) add a separate mechanical field to `ScheduleBlock` (e.g. `action`) and keep `activity` as flavor text;
  - (b) a label→action mapping table stored as data;
  - (c) rewrite the roster labels to the fixed vocabulary.
- I recommend (a): the narrative labels survive and the mapping becomes explicit. Once it's decided, the loader should also reject unmapped activities.

**Needs something from Henry:** add the `com.unity.nuget.newtonsoft-json` package on the Editor machine. Without it the loader and its checks won't compile in Unity. None of this has been run in Unity.

**Judgment calls:**
1. Used Newtonsoft.Json instead of hand-rolling a parser. Unity's package 3.2.2 bundles 13.0.2 (checked against Unity's registry), so 13.0.2 is pinned here too.
2. `setup-intellisense.sh` now rewrites the csproj on every run instead of skipping when it exists, so the new package reference reaches existing Codespaces.
3. Parser hardening: dates stay as strings (`DateParseHandling.None`), duplicate keys are an error, and trailing content is rejected. Enums accept exact names only, NaN/Infinity are rejected, and time strings are anchored with `\z`.
4. Validation beyond the four required categories:
   - §7's documented ranges.
   - Non-empty strings.
   - Whole numbers for age, money, and income, each ≥ 0. I allow money = 0 even though the README says "positive", because the add-npc skill treats a broke NPC as valid. The age and income floors are my own addition.
   - Home must be a residence; otherwise Sleep and Bathe would be infeasible forever.
   - A block with start == end is rejected, since it would read as a 24h block.
   - `lastSimulatedAt` must be on or after the clock epoch.
   - No relationship with yourself.
   - `memoryStream.involvedNpcIds` are cross-checked just like relationships.
5. **Not enforced by the loader:** schedule overlap, one primary sleep block, Rival/Family/Romantic reciprocity, §21 job counts. The roster session's throwaway validator checked those once.
6. The `job` key is required but may be `null`. `$`-prefixed keys are ignored at the top level only, per the README; nested ones are unknown-field errors.
7. The top-level `player` section is optional. The roster has none, so it's only exercised with inline JSON. `"player": null` is an error.
8. Duplicate-key errors come from the JSON parser, so their `Path` is `$`; the field path appears only inside the message text.
9. Rosa's day starts at her authored `lastSimulatedAt` (Day 255 22:00), not Day 1 00:00, so her needs match their timestamp.
10. Check 9's work guardrail was generalized from a hardcoded "of 8 hours" to "hours the AI saw as scheduled work (> 0, and ≥ 75% worked)". That generalized version is the one Rosa fails.

Still uncommitted.
