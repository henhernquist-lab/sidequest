---
name: add-npc
description: Scaffolds a new Side Quest NPC with every field from the data model populated (id, name, age, personalityTraits, needs, job, home, inventory, money, schedule, goals, relationships, memoryStream, lodTier) — never a partially-formed one, since a missing field silently reads as zero/false to the utility-AI scorer instead of erroring. Use whenever adding an NPC, creating a character/resident, or extending the NPC roster — triggers on requests like "add an NPC," "create a character," "new resident," or "add a townsperson."
---

# add-npc

Reference: `docs/design-doc.md` §7 (NPC Data Model) is the source of truth for the schema. Re-read it if this skill and the design doc ever disagree — the design doc wins.

## Why this skill exists

A missing field on an NPC doesn't throw. It reads as a default (usually zero/false) to the utility-AI scorer (§8), which means an incomplete NPC doesn't crash — it just behaves subtly wrong (unmotivated, broke, friendless, never scheduled) in a way that's easy to miss in testing. This skill's job is to make that structurally impossible: every new NPC gets every field, explicitly set, every time.

## What to do when scaffolding a new NPC

1. Open `docs/design-doc.md` §7 and confirm the current field list before writing code — the schema is allowed to evolve, and this skill should follow it, not a cached memory of it.
2. Generate/populate **every** field, with no silent defaults:
   - `id` — stable unique identifier (don't reuse or auto-increment carelessly; check for collisions against existing NPCs).
   - `name`, `age`
   - `personalityTraits` — all trait dimensions the schema defines (e.g. sociability, conscientiousness, temper, riskTolerance), each explicitly set in range, not left at a language-default 0.
   - `needs` — the full needs vector (hunger, energy, social, hygiene, fun, moneyPressure), each explicitly initialized, not just declared.
   - `job` — title, workplaceId, shiftSchedule. If the NPC is unemployed, that's a deliberate explicit state, not an unset field.
   - `home` — a valid locationId that actually exists in the town data.
   - `inventory` — explicit list, even if empty (`[]`, not null/undefined).
   - `money` — explicit starting value, not an implicit zero.
   - `schedule` — a real data-driven timetable entry, not a stub that will silently no-op in the utility layer.
   - `goals` — explicit list (commonly empty at creation), with the field present.
   - `relationships` — explicit map, even if empty at creation.
   - `memoryStream` — explicit empty list at creation (never null).
   - `lodTier` — an explicit initial tier (usually `dormant` or `background` for a freshly created NPC not currently near the player) plus a last-simulated timestamp.
3. After scaffolding, do a field-by-field diff against the §7 list out loud (in the response, not just in code) and confirm nothing was skipped — this is the actual check, not just "I used a constructor with named args so it's probably fine."
4. If any field's correct value depends on a design decision Henry owns (e.g. a new personality trait dimension, a change to the needs vector shape) — don't invent it. Flag it and use the existing schema as-is.

## What NOT to do

- Don't add a "temporary" NPC with some fields TODO'd in — half-formed NPCs are exactly the failure mode this skill exists to prevent, even as a placeholder.
- Don't assume a sensible-looking default (e.g. `money: 0`) is fine without explicitly deciding it's fine — a bankrupt NPC is a valid design choice, an accidentally-zero NPC is a bug.
