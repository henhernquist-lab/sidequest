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
