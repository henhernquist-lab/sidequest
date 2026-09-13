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
| schedule `actionType`, `activity` | Required exact enum name: `Eat`, `Sleep`, `Socialize`, `Bathe`, `WorkShift`, `PursueGoal`, `Idle`. Only `actionType` drives dispatch; `activity` is display/flavor text. |
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

The permanent roster loader rejects unknown/missing fields, invalid enum names, ranges,
unknown ids/locations, overlapping schedules (including midnight wraps), and non-mutual
Family/Rival/Romantic types. The historical content audit also checked primary sleep
blocks and job counts; those two content rules are not loader requirements.

Run the headless verification suite from the repository root:

```sh
dotnet run --project tools/Verification
```

The suite includes corrupted-roster controls and loaded day timelines. See `STATUS.md`
for current results and any failing behavioral guardrails. Unity wiring and play behavior
require verification on the Editor machine.
