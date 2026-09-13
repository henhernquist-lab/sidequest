using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using SideQuest.Data;

namespace SideQuest.Data.Verification
{
    // Field-completeness self-check backing the add-npc skill's rule: every NPC
    // field must be explicitly populated at creation, since a missing field
    // silently reads as zero/false to the utility-AI scorer instead of
    // erroring (design-doc.md §7 / the add-npc skill).
    public static class NpcDataModelVerification
    {
        public static NPC BuildFullyPopulatedNpc()
        {
            return new NPC
            {
                Id = "npc_mara_01",
                DisplayName = "Mara Whitfield",
                Age = 34,
                AppearanceRef = "npc_mara_skin_v1",
                Personality = new Personality
                {
                    Ambition = 0.7f,
                    Extroversion = 0.6f,
                    Honesty = 0.8f,
                    RiskTolerance = 0.3f,
                    Warmth = 0.65f,
                    Diligence = 0.55f
                },
                Needs = new Needs
                {
                    Hunger = 40f,
                    Energy = 70f,
                    Social = 55f,
                    Hygiene = 80f
                },
                Job = new Job
                {
                    Title = "Barista",
                    WorkplaceId = "loc_corner_diner",
                    Income = 320,
                    Performance = 0.75f
                },
                HomeLocationId = "loc_apartment_complex_3b",
                InventoryItemIds = new List<string> { "item_apron", "item_house_key" },
                Money = 540,
                Schedule = new List<ScheduleBlock>
                {
                    new ScheduleBlock { Start = TimeSpan.FromHours(7), End = TimeSpan.FromHours(15), Activity = "Work", LocationId = "loc_corner_diner" },
                    new ScheduleBlock { Start = TimeSpan.FromHours(15), End = TimeSpan.FromHours(22), Activity = "Leisure", LocationId = "loc_apartment_complex_3b" }
                },
                Goals = new Goals
                {
                    ShortTerm = new List<string> { "Save $200 for rent" },
                    LongTerm = new List<string> { "Get promoted to shift lead" }
                },
                Relationships = new Dictionary<string, Relationship>
                {
                    ["npc_devon_02"] = new Relationship
                    {
                        Affinity = 0.5f,
                        Trust = 0.4f,
                        Familiarity = 0.3f,
                        LastInteractionAt = new DateTime(2026, 9, 10, 9, 0, 0, DateTimeKind.Utc),
                        Type = RelationshipType.Acquaintance
                    }
                },
                MemoryStream = new List<MemoryFact>
                {
                    new MemoryFact
                    {
                        Timestamp = new DateTime(2026, 9, 10, 8, 30, 0, DateTimeKind.Utc),
                        EventType = "Conversation",
                        Description = "Talked with Devon about the diner's new espresso machine.",
                        ImportanceScore = 0.2f,
                        InvolvedNpcIds = new List<string> { "npc_devon_02" }
                    }
                },
                CurrentTier = SimTier.Active,
                LastSimulatedAt = new DateTime(2026, 9, 11, 7, 0, 0, DateTimeKind.Utc),
                CurrentLocationId = "loc_corner_diner",
                CurrentActivity = "Working"
            };
        }

        public static PlayerCharacter BuildFullyPopulatedPlayerCharacter()
        {
            return new PlayerCharacter
            {
                DisplayName = "Henry",
                Money = 150,
                InventoryItemIds = new List<string> { "item_phone_giggo" },
                Karma = 0.1f,
                Notoriety = 0.0f,
                GigRating = 4.8f,
                CompletedGigIds = new List<string> { "gig_gnome_heist" },
                CurrentLocationId = "loc_town_square"
            };
        }

        // Negative control: several fields left at their C# defaults (null),
        // exactly the failure mode the add-npc skill exists to prevent. Exists
        // only to prove the checker below actually detects a missing field,
        // not just to prove the good case passes.
        public static NPC BuildIncompleteNpc()
        {
            return new NPC
            {
                Id = "npc_broken_01",
                DisplayName = "Incomplete NPC"
                // Personality, Needs, Job, HomeLocationId, Goals, and everything
                // else below is deliberately left unset.
            };
        }

        public class FieldCheckResult
        {
            public bool Passed = true;
            public List<string> MissingFields = new();
        }

        // Reflection-based completeness check over reference-type fields
        // (string, class, List<T>, Dictionary<K,V>) — must be non-null,
        // recursing into nested model types. Value-type fields (int, float,
        // enum, DateTime, TimeSpan) can't be distinguished from "explicitly
        // set to 0/default" by reflection alone, so those are spot-checked
        // with explicit equality assertions in RunSelfCheck instead.
        public static FieldCheckResult CheckAllReferenceFieldsPopulated(object instance, string path)
        {
            var result = new FieldCheckResult();
            CheckRecursive(instance, path, result, new HashSet<object>());
            return result;
        }

        private static void CheckRecursive(object instance, string path, FieldCheckResult result, HashSet<object> visited)
        {
            if (instance == null)
            {
                result.Passed = false;
                result.MissingFields.Add(path);
                return;
            }

            var type = instance.GetType();

            if (type.IsPrimitive || type == typeof(string) || type.IsEnum ||
                type == typeof(DateTime) || type == typeof(TimeSpan))
            {
                return;
            }

            if (type.IsValueType)
            {
                // e.g. a boxed KeyValuePair<,> from enumerating a Dictionary directly —
                // shouldn't normally reach here since dictionaries are handled below,
                // but bail out safely rather than reflecting into compiler-generated fields.
                return;
            }

            if (!visited.Add(instance))
            {
                return; // avoid infinite loops on cyclic references
            }

            if (instance is IDictionary dictionary)
            {
                foreach (var value in dictionary.Values)
                {
                    CheckRecursive(value, path + "{}", result, visited);
                }
                return;
            }

            if (instance is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    CheckRecursive(item, path + "[]", result, visited);
                }
                return;
            }

            foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                if (field.FieldType.IsValueType)
                {
                    continue; // checked explicitly elsewhere — see comment on CheckAllReferenceFieldsPopulated
                }

                var value = field.GetValue(instance);
                var fieldPath = string.IsNullOrEmpty(path) ? field.Name : $"{path}.{field.Name}";

                if (value == null)
                {
                    result.Passed = false;
                    result.MissingFields.Add(fieldPath);
                    continue;
                }

                CheckRecursive(value, fieldPath, result, visited);
            }
        }

        // Runs the success case AND the negative control and returns true only
        // if the fully-populated instances pass AND the deliberately incomplete
        // NPC is correctly flagged as incomplete — per AGENTS.md's verification
        // discipline: prove it with a control, not just "it looked right once."
        public static bool RunSelfCheck(out List<string> log)
        {
            log = new List<string>();
            bool allOk = true;

            var goodNpc = BuildFullyPopulatedNpc();
            var goodPlayer = BuildFullyPopulatedPlayerCharacter();
            var badNpc = BuildIncompleteNpc();

            var goodNpcResult = CheckAllReferenceFieldsPopulated(goodNpc, nameof(NPC));
            log.Add($"[success case] fully-populated NPC reference-field check: {(goodNpcResult.Passed ? "PASS" : "FAIL")}" +
                    (goodNpcResult.Passed ? "" : $" — missing: {string.Join(", ", goodNpcResult.MissingFields)}"));
            if (!goodNpcResult.Passed) allOk = false;

            var goodPlayerResult = CheckAllReferenceFieldsPopulated(goodPlayer, nameof(PlayerCharacter));
            log.Add($"[success case] fully-populated PlayerCharacter reference-field check: {(goodPlayerResult.Passed ? "PASS" : "FAIL")}" +
                    (goodPlayerResult.Passed ? "" : $" — missing: {string.Join(", ", goodPlayerResult.MissingFields)}"));
            if (!goodPlayerResult.Passed) allOk = false;

            var badNpcResult = CheckAllReferenceFieldsPopulated(badNpc, nameof(NPC));
            bool controlCorrectlyFailed = !badNpcResult.Passed;
            log.Add($"[control case] deliberately incomplete NPC correctly detected as incomplete: {(controlCorrectlyFailed ? "PASS" : "FAIL — checker did not catch it!")}" +
                    (badNpcResult.MissingFields.Count > 0 ? $" — detected missing: {string.Join(", ", badNpcResult.MissingFields)}" : ""));
            if (!controlCorrectlyFailed) allOk = false;

            bool valuesOk = goodNpc.Age == 34
                && goodNpc.Money == 540
                && goodNpc.CurrentTier == SimTier.Active
                && goodNpc.Personality.Ambition == 0.7f
                && goodNpc.Needs.Hunger == 40f
                && goodNpc.Job.Income == 320
                && goodPlayer.Karma == 0.1f
                && goodPlayer.GigRating == 4.8f;
            log.Add($"[success case] explicit value-field spot check on fully-populated instances: {(valuesOk ? "PASS" : "FAIL")}");
            if (!valuesOk) allOk = false;

            return allOk;
        }
    }
}
