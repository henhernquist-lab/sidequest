# Assets/Data — roster & content data

## npc_roster.json

18 hand-authored NPCs for the MVP town. Field-for-field JSON mirror of the finalized
`NPC` schema in `docs/design-doc.md` §7 (scaffolded in C# under `Assets/Scripts/Data/NPC.cs`).
The design doc is the source of truth — if this file and §7 disagree, §7 wins and this file
is wrong.

The player is **not** in this file (`PlayerCharacter` is defined elsewhere, §7).

### Top-level shape

- `$schema_note`, `$notes` — documentation keys, **not data**. Any loader must ignore
  top-level keys starting with `$`.
- `npcs` — list of 18 NPC objects.

### Conventions

| Field | Convention |
|---|---|
| `id` | Stable, format `npc_<name>_<role-or-vibe>` (e.g. `npc_rosa_shop`). Never reused. |
| `displayName`, `age`, `appearanceRef` | As §7. `appearanceRef` points at a future `characters/…` Blockbench asset. |
| `personality` | All six traits present, 0.0–1.0, explicitly chosen (no defaults). |
| `needs` | Exactly the four §7 fields (`hunger`, `energy`, `social`, `hygiene`), 0–100. **No** money/fun field — money lives on `NPC.Money`. |
| `job` | `null` for the 14 unemployed/student/retired NPCs. The 4 employed NPCs hold the §21 jobs, one each, with `workplaceId` matching the §20 location. |
| `homeLocationId`, `currentLocationId`, schedule `locationId` | One of the six §20 MVP locations only: `loc_corner_diner`, `loc_general_store`, `loc_auto_shop`, `loc_police_station`, `loc_apartment_complex`, `loc_town_square`. |
| `inventoryItemIds` | `item_*` strings are **forward-declared** — no item registry exists yet. Treat as opaque stable ids. |
| `money` | Starting value in whatever unit the economy settles on; positive integers. |
| `schedule` | Times `HH:MM` 24-hour local. `end < start` means the block wraps past midnight. Every NPC has exactly one primary (≥5h) sleep block; blocks must not overlap. |
| `goals` | `shortTerm` / `longTerm` string lists; flavor + future utility-AI hooks, not yet mechanical. |
| `relationships` | Keyed by other NPC's `id` (mirrors §7's `Dictionary<string, Relationship>`). ≥2 entries per NPC. Types are the §7 enum. `Rival`/`Family`/`Romantic` links are reciprocal (both sides list each other with the same type); one-sided *feelings* are expressed through asymmetric `affinity` values, not through asymmetric `type`. |
| `memoryStream` | 1–2 seeded `MemoryFact`s per NPC as starting history. Timestamps ISO-8601 UTC (`Z`). |
| `currentTier` | Sim placement at authoring time: 9 `Active` / 9 `Background`, no `Dormant` yet (nobody has been away long enough). |
| `lastSimulatedAt`, `currentActivity` | Roster-authoring snapshot ("day 0"); loaders are free to re-simulate from here. |

### Design intent baked into the roster

See the `$notes` array in the JSON itself for the tension web (Rival/Family/Romantic
pairs), the §22 job-loss seed, and the gossip-hub character. Summary: 2 rival pairs,
5 family pairs, 1 mutual romantic pair, plus one deliberate one-sided crush expressed
as asymmetric affinity.

### Validation status

Validated by a throwaway script (not committed): exact field-set match against §7 per
NPC, all range checks, all id/location references resolve within the file, schedule
overlap + single-sleep-block checks, reciprocity of Rival/Family/Romantic links,
§21 job counts, §22 seed presence. Control test: 6 deliberately injected bugs were all
caught by the same checks — see the 2026-09-13 roster entry in `STATUS.md`.

If you hand-edit this file: keep the field set exact (extra fields are errors, not
tolerated extras), keep every referenced id resolvable, and re-run whatever validation
exists at that time. When a real loader gets written it should **fail loudly** on
unknown fields, unknown locations, and unknown ids — a silently-defaulted NPC is
design-doc §15 Implementation Risk #5 (see the `add-npc` skill).
