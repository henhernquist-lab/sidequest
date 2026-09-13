---
name: add-npc
description: Scaffolds a new Side Quest NPC or PlayerCharacter with every field from the finalized data model populated (NPC's Id, DisplayName, Age, AppearanceRef, Personality, Needs, Job, HomeLocationId, InventoryItemIds, Money, Schedule, Goals, Relationships, MemoryStream, CurrentTier, LastSimulatedAt, CurrentLocationId, CurrentActivity — plus PlayerCharacter's Karma, Notoriety, GigRating) — never a partially-formed one, since a missing field silently reads as zero/false to the utility-AI scorer instead of erroring. Use whenever adding an NPC, creating a character/resident, working with the player character, or extending the roster — triggers on requests like "add an NPC," "create a character," "new resident," "add a townsperson," or "player character."
---

# add-npc

Reference: `docs/design-doc.md` §7 (Data Model) is the source of truth for the schema. Re-read it if this skill and the design doc ever disagree — the design doc wins. The finalized schema is also scaffolded as real C# in `Assets/Scripts/Data/` (one class/enum per file) — treat those files as the schema's concrete expression, and `Assets/Scripts/Verification/NpcDataModelVerification.cs` as a worked example of building one fully-populated instance of each.

## Why this skill exists

A missing field on an NPC doesn't throw. It reads as a default (usually zero/false/null) to the utility-AI scorer (§8), which means an incomplete NPC doesn't crash — it just behaves subtly wrong (unmotivated, broke, friendless, never scheduled) in a way that's easy to miss in testing. This skill's job is to make that structurally impossible: every new `NPC` or `PlayerCharacter` gets every field, explicitly set, every time.

## NPC — what to populate

1. Open `docs/design-doc.md` §7 (or `Assets/Scripts/Data/NPC.cs`) and confirm the current field list before writing code — the schema is allowed to evolve, and this skill should follow it, not a cached memory of it.
2. Populate **every** field, with no silent defaults:
   - `Id` — stable unique identifier (don't reuse or auto-increment carelessly; check for collisions against existing NPCs).
   - `DisplayName`, `Age`, `AppearanceRef` (points to a model/skin asset).
   - `Personality` — a real `Personality` instance with all six traits set (`Ambition`, `Extroversion`, `Honesty`, `RiskTolerance`, `Warmth`, `Diligence`), each explicitly in 0.0–1.0, not left at a language-default 0.
   - `Needs` — a real `Needs` instance with all four fields set (`Hunger`, `Energy`, `Social`, `Hygiene`), each explicitly initialized (0–100 scale), not just declared. Note: `Needs` does **not** have a money-related field — money pressure is not part of the needs vector; it's tracked via `Money` directly.
   - `Job` — nullable by design (unemployed NPCs are a real, playable state, not an edge case). If employed, populate `Title`, `WorkplaceId`, `Income`, `Performance`. If unemployed, that's a deliberate explicit `null`, not an unset field — say so, don't leave it ambiguous.
   - `HomeLocationId` — a valid location id that actually exists in the town data.
   - `InventoryItemIds` — explicit list, even if empty (`new()`, not left null).
   - `Money` — explicit starting value, not an implicit zero.
   - `Schedule` — a real `List<ScheduleBlock>`, each block with `Start`, `End`, `Activity`, `LocationId` set — not a stub that will silently no-op in the utility layer.
   - `Goals` — a real `Goals` instance with `ShortTerm`/`LongTerm` lists present (commonly empty at creation, but the field itself must be a real object, not null).
   - `Relationships` — explicit `Dictionary<string, Relationship>`, even if empty at creation.
   - `MemoryStream` — explicit empty `List<MemoryFact>` at creation (never null).
   - `CurrentTier` — an explicit initial `SimTier` (usually `Dormant` or `Background` for a freshly created NPC not currently near the player), plus `LastSimulatedAt`.
   - `CurrentLocationId`, `CurrentActivity` — explicit current state, not left blank.
3. After scaffolding, do a field-by-field diff against the list above out loud (in the response, not just in code) and confirm nothing was skipped — this is the actual check, not just "I used an object initializer so it's probably fine."
4. If any field's correct value depends on a design decision Henry owns (e.g. a new personality trait dimension, a change to the needs vector shape) — don't invent it. Flag it and use the existing schema as-is.

## PlayerCharacter — what to populate

A separate class from `NPC` — Karma, Notoriety, and GigRating are **player-only** stats; they do not exist on `NPC` and should never be added there. Populate:

- `DisplayName`, `Money`, `InventoryItemIds` (explicit list, even if empty).
- `Karma` (-1.0 cruel to 1.0 kind), `Notoriety` (0.0–1.0 public chaos/crime level), `GigRating` (0.0–5.0, Uber-style) — all three explicit, not left at an accidental 0 that reads as "neutral" when that wasn't a deliberate choice.
- `CompletedGigIds` — explicit list, even if empty.
- `CurrentLocationId`.

## What NOT to do

- Don't add a "temporary" NPC or PlayerCharacter with some fields TODO'd in — half-formed instances are exactly the failure mode this skill exists to prevent, even as a placeholder.
- Don't assume a sensible-looking default (e.g. `Money = 0`) is fine without explicitly deciding it's fine — a bankrupt NPC is a valid design choice, an accidentally-zero NPC is a bug.
- Don't put `Karma`/`Notoriety`/`GigRating` on `NPC`, or job/schedule/relationship-graph fields on `PlayerCharacter` — the two classes are deliberately separate (design-doc §7: "Karma and Notoriety are player-facing morality/chaos stats, not something every NPC tracks about itself").
