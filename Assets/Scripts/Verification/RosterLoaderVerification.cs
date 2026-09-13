using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;
using SideQuest.Core;
using SideQuest.Data;
using SideQuest.Data.Verification;
using SideQuest.Simulation;
using SideQuest.Simulation.Verification;
using SideQuest.World;

namespace SideQuest.Loading.Verification
{
    // Headless checks for RosterLoader against the real Assets/Data/npc_roster.json. Every failure case is paired
    // with the unmodified file loading cleanly, so a "caught" error can't just be the loader rejecting everything.
    public static class RosterLoaderVerification
    {
        private const string RosaPath = "npcs[1](npc_rosa_shop)";
        private const string RosaNeeds = "\"needs\": { \"hunger\": 65.0, \"energy\": 60.0, \"social\": 70.0, \"hygiene\": 75.0 }";

        public static bool RunAll(List<string> log, string rosterPath)
        {
            string json = File.ReadAllText(rosterPath);
            var locations = MvpTownData.CreateRegistry();

            bool ok = true;
            ok &= Section(log, "R1. Real roster loads: count, jobs, Rosa's values", l => VerifyValidRoster(l, json, rosterPath, locations));
            ok &= Section(log, "R2. Corrupted copies fail loudly with the exact error; the unmodified file doesn't", l => VerifyCorruptions(l, json, locations));
            ok &= Section(log, "R3. Parsing traps that would otherwise let bad values through silently", l => VerifyParsingTraps(l, json, locations));
            ok &= Section(log, "R4. Optional player section", l => VerifyPlayerSection(l, locations));
            ReportScheduleVocabulary(log, json, rosterPath, locations);
            ok &= Section(log, "R6. One full day for Rosa, from her loaded data", l => VerifyRosaDay(l, json, rosterPath, locations));
            ok &= Section(log, "R7. Typed schedules, overlap and mutuality controls", l => VerifyScheduleValidation(l, json, locations));
            foreach (string id in new[] { "npc_ana_diner", "npc_sal_mechanic", "npc_frank_cop" })
            {
                ok &= Section(log, "R8. Loaded work day: " + id, l =>
                {
                    NPC npc = RosterLoader.Load(json, rosterPath, locations).NpcsById[id];
                    return SimulationVerification.RunDayTimeline(l, npc, GameTime.FromDateTime(npc.LastSimulatedAt), 42, locations);
                });
            }
            return ok;
        }

        private static bool Section(List<string> log, string title, Func<List<string>, bool> check)
        {
            log.Add("");
            log.Add($"=== {title} ===");
            bool passed = check(log);
            log.Add(passed ? "RESULT: PASS" : "RESULT: FAIL");
            return passed;
        }

        private static bool Expect(List<string> log, bool condition, string description)
        {
            log.Add($"  [{(condition ? " ok " : "FAIL")}] {description}");
            return condition;
        }

        private static bool VerifyValidRoster(List<string> log, string json, string rosterPath, LocationRegistry locations)
        {
            Roster roster;
            try
            {
                roster = RosterLoader.Load(json, rosterPath, locations);
            }
            catch (RosterLoadException ex)
            {
                return Expect(log, false, "the real roster should load cleanly, but: " + ex.Message);
            }

            // An independent read of the same file, with no loader mapping in between.
            var rawNpcs = (JArray)JObject.Parse(json)["npcs"];
            JObject Raw(string id) => rawNpcs.Cast<JObject>().Single(n => (string)n["id"] == id);

            bool ok = Expect(log, roster.Npcs.Count == 18 && roster.Npcs.Count == rawNpcs.Count,
                $"loaded {roster.Npcs.Count} NPCs (raw JSON array has {rawNpcs.Count}; README says 18)");

            var expectedJobs = new[]
            {
                (Id: "npc_ana_diner", Title: "Barista/Cook", Workplace: MvpTownData.CornerDiner),
                (Id: "npc_rosa_shop", Title: "Shopkeeper", Workplace: MvpTownData.GeneralStore),
                (Id: "npc_sal_mechanic", Title: "Mechanic", Workplace: MvpTownData.AutoShop),
                (Id: "npc_frank_cop", Title: "Cop", Workplace: MvpTownData.PoliceStation),
            };

            var employed = roster.Npcs.Where(n => n.Job != null).ToList();
            foreach (var npc in employed)
                log.Add($"  {npc.Id,-18} {npc.Job.Title,-13} @ {npc.Job.WorkplaceId,-20} income {npc.Job.Income,3}   performance {npc.Job.Performance}");

            bool rightHolders = employed.Select(n => n.Id).OrderBy(x => x, StringComparer.Ordinal)
                .SequenceEqual(expectedJobs.Select(j => j.Id).OrderBy(x => x, StringComparer.Ordinal));
            ok &= Expect(log, rightHolders, $"the employed NPCs are exactly the 4 §21 job holders (found {employed.Count})");

            bool jobsMatch = true;
            foreach (var expected in expectedJobs)
            {
                var rawJob = Raw(expected.Id)["job"] as JObject;
                jobsMatch &= roster.NpcsById.TryGetValue(expected.Id, out NPC holder) && holder.Job != null && rawJob != null
                    && holder.Job.Title == expected.Title && holder.Job.WorkplaceId == expected.Workplace
                    && holder.Job.Title == (string)rawJob["title"] && holder.Job.WorkplaceId == (string)rawJob["workplaceId"]
                    && holder.Job.Income == (int)rawJob["income"] && holder.Job.Performance == (float)(double)rawJob["performance"];
            }
            ok &= Expect(log, jobsMatch, "each title and workplace matches §21, and every job field matches the raw JSON");

            var unemployed = roster.Npcs.Where(n => n.Job == null).ToList();
            ok &= Expect(log, unemployed.Count == 14 && unemployed.All(n => Raw(n.Id)["job"].Type == JTokenType.Null),
                $"the other {unemployed.Count} have Job == null, each an explicit null in the JSON rather than a missing key");

            NPC rosa = roster.NpcsById["npc_rosa_shop"];
            JObject rawRosa = Raw("npc_rosa_shop");
            log.Add($"  Rosa loaded : Job.Performance = {rosa.Job.Performance}, Personality.Diligence = {rosa.Personality.Diligence}");
            log.Add($"  Rosa in JSON: job.performance = {rawRosa["job"]["performance"]}, personality.diligence = {rawRosa["personality"]["diligence"]}");
            ok &= Expect(log, rosa.Job.Performance == (float)(double)rawRosa["job"]["performance"] && rosa.Job.Performance == 0.27f,
                "Rosa's Performance matches the JSON (0.27)");
            ok &= Expect(log, rosa.Personality.Diligence == (float)(double)rawRosa["personality"]["diligence"] && rosa.Personality.Diligence == 0.25f,
                "Rosa's Diligence matches the JSON (0.25)");

            var incomplete = new List<string>();
            foreach (var npc in roster.Npcs)
            {
                var result = NpcDataModelVerification.CheckAllReferenceFieldsPopulated(npc, npc.Id);
                // An unemployed NPC's null Job is correct (§7), and the check above confirms it came from the JSON.
                incomplete.AddRange(result.MissingFields.Where(f => !(f == npc.Id + ".Job" && npc.Job == null)));
            }
            ok &= Expect(log, incomplete.Count == 0, incomplete.Count == 0
                ? "every loaded NPC has every reference field populated"
                : "missing: " + string.Join(", ", incomplete));

            bool actionsMatch = rawNpcs.OfType<JObject>().All(raw =>
            {
                NPC npc = roster.NpcsById[(string)raw["id"]];
                return npc.Schedule.Select((block, i) => block.ActionType.ToString() == (string)raw["schedule"][i]["actionType"]
                    && block.Activity == (string)raw["schedule"][i]["activity"]).All(match => match);
            });
            ok &= Expect(log, actionsMatch, "all ActionType values and flavor labels match raw JSON exactly");

            var sizeMismatches = new List<string>();
            foreach (JObject raw in rawNpcs)
            {
                NPC npc = roster.NpcsById[(string)raw["id"]];
                if (npc.Schedule.Count != ((JArray)raw["schedule"]).Count) sizeMismatches.Add(npc.Id + ".schedule");
                if (npc.Relationships.Count != ((JObject)raw["relationships"]).Count) sizeMismatches.Add(npc.Id + ".relationships");
                if (npc.MemoryStream.Count != ((JArray)raw["memoryStream"]).Count) sizeMismatches.Add(npc.Id + ".memoryStream");
                if (npc.InventoryItemIds.Count != ((JArray)raw["inventoryItemIds"]).Count) sizeMismatches.Add(npc.Id + ".inventoryItemIds");
            }
            ok &= Expect(log, sizeMismatches.Count == 0, sizeMismatches.Count == 0
                ? "schedule, relationship, memory, and inventory counts match the JSON for all 18 NPCs"
                : "count mismatch: " + string.Join(", ", sizeMismatches));

            ok &= Expect(log, roster.Player == null, "the file has no player section, so Player is null (the README says the player lives elsewhere)");
            return ok;
        }

        private static bool VerifyCorruptions(List<string> log, string json, LocationRegistry locations)
        {
            var badLocation = (Find: "\"workplaceId\": \"loc_general_store\"", Replace: "\"workplaceId\": \"loc_genral_store\"");
            var badRelationship = (Find: "\"npc_priya_florist\": { \"affinity\": 0.7,", Replace: "\"npc_priya_flourist\": { \"affinity\": 0.7,");
            var missingField = (Find: ",\n        \"diligence\": 0.25", Replace: "");
            var unknownField = (Find: RosaNeeds, Replace: RosaNeeds.Replace("75.0 }", "75.0, \"fun\": 40.0 }"));

            bool ok;
            try
            {
                RosterLoader.Load(json, "npc_roster.json", locations);
                ok = Expect(log, true, "control: the unmodified file loads with 0 errors");
            }
            catch (RosterLoadException ex)
            {
                ok = Expect(log, false, "control: the unmodified file should load, but: " + ex.Message);
            }

            ok &= ExpectLoadFailure(log, "bad location id: Rosa's workplace 'loc_general_store' -> 'loc_genral_store'",
                Corrupt(json, badLocation), locations,
                (RosaPath + ".job.workplaceId", "unknown location id 'loc_genral_store'"));

            ok &= ExpectLoadFailure(log, "relationship to a nonexistent NPC: Rosa -> 'npc_priya_flourist'",
                Corrupt(json, badRelationship), locations,
                (RosaPath + ".relationships.npc_priya_flourist", "'npc_priya_flourist' is not an NPC id"),
                ("npcs[7](npc_priya_florist).relationships.npc_rosa_shop.type", "non-mutual Family link"));

            log.Add("  (a silent default for the next one would give Rosa Diligence 0.0, which changes when her §22 storyline fires)");
            ok &= ExpectLoadFailure(log, "missing required field: Rosa's personality.diligence removed",
                Corrupt(json, missingField), locations,
                (RosaPath + ".personality.diligence", "missing required field"));

            ok &= ExpectLoadFailure(log, "unknown field: a 'fun' need added to Rosa (the old 6-need schema)",
                Corrupt(json, unknownField), locations,
                (RosaPath + ".needs.fun", "unknown field"));

            ok &= ExpectLoadFailure(log, "the first three corruptions together in one file",
                Corrupt(Corrupt(Corrupt(json, badLocation), badRelationship), missingField), locations,
                (RosaPath + ".job.workplaceId", "unknown location id 'loc_genral_store'"),
                (RosaPath + ".relationships.npc_priya_flourist", "'npc_priya_flourist' is not an NPC id"),
                ("npcs[7](npc_priya_florist).relationships.npc_rosa_shop.type", "non-mutual Family link"),
                (RosaPath + ".personality.diligence", "missing required field"));
            return ok;
        }

        private static bool VerifyParsingTraps(List<string> log, string json, LocationRegistry locations)
        {
            bool tryParseAccepts = Enum.TryParse("0", out SimTier parsedTier);
            log.Add($"  Why these matter, shown against the naive approach:");
            log.Add($"    Enum.TryParse(\"0\") returns {tryParseAccepts} and yields SimTier.{parsedTier}");
            log.Add($"    NaN < 0 is {double.NaN < 0}, NaN > 100 is {double.NaN > 100}, so a plain range check passes NaN");
            log.Add($"    Regex ^HH:MM$ matches \"08:30\\n\": {Regex.IsMatch("08:30\n", @"^([01][0-9]|2[0-3]):([0-5][0-9])$")}");
            bool zeroLengthBlockCoversAll = ScheduleService.Contains(
                new ScheduleBlock { Start = TimeSpan.FromHours(12), End = TimeSpan.FromHours(12) }, TimeSpan.FromHours(3));
            log.Add($"    a 12:00-12:00 block contains 03:00: {zeroLengthBlockCoversAll}");

            const string rosaTier = "\"currentTier\": \"Active\",\n      \"lastSimulatedAt\": \"2026-09-12T22:00:00Z\",\n      \"currentLocationId\": \"loc_apartment_complex\",\n      \"currentActivity\": \"Sleeping\"\n    },\n    {\n      \"id\": \"npc_sal_mechanic\"";

            bool ok = ExpectLoadFailure(log, "duplicate key: Rosa's needs lists hunger twice",
                Corrupt(json, (RosaNeeds, RosaNeeds.Replace("\"hunger\": 65.0,", "\"hunger\": 65.0, \"hunger\": 5.0,"))), locations,
                ("$", "already exists"));

            ok &= ExpectLoadFailure(log, "numeric enum string: Rosa's currentTier \"Active\" -> \"0\"",
                Corrupt(json, (rosaTier, rosaTier.Replace("\"Active\"", "\"0\""))), locations,
                (RosaPath + ".currentTier", "must be one of"));

            ok &= ExpectLoadFailure(log, "NaN: Rosa's hunger 65.0 -> NaN",
                Corrupt(json, (RosaNeeds, RosaNeeds.Replace("65.0", "NaN"))), locations,
                (RosaPath + ".needs.hunger", "must be a number from 0 to 100"));

            ok &= ExpectLoadFailure(log, "trailing newline in a time: Rosa's first block start \"08:30\" -> \"08:30\\n\"",
                Corrupt(json, ("{ \"start\": \"08:30\", \"end\": \"09:00\", \"activity\": \"Open the store (late)\"",
                               "{ \"start\": \"08:30\\n\", \"end\": \"09:00\", \"activity\": \"Open the store (late)\"")), locations,
                (RosaPath + ".schedule[0].start", "must be a 24-hour HH:MM"));

            ok &= ExpectLoadFailure(log, "zero-length block: Rosa's 12:00-13:00 lunch -> 12:00-12:00",
                Corrupt(json, ("\"end\": \"13:00\", \"activity\": \"Long lunch / errands\"",
                               "\"end\": \"12:00\", \"activity\": \"Long lunch / errands\"")), locations,
                (RosaPath + ".schedule[2].end", "equals start"));

            ok &= ExpectLoadFailure(log, "trailing content after the root object",
                json + "\n{ \"npcs\": [] }\n", locations,
                ("$", "after"));
            return ok;
        }

        private static bool VerifyPlayerSection(List<string> log, LocationRegistry locations)
        {
            const string validPlayer = @"{ ""npcs"": [], ""player"": { ""displayName"": ""Test Player"", ""money"": 150, ""inventoryItemIds"": [""item_phone""],
  ""karma"": 0.1, ""notoriety"": 0.0, ""gigRating"": 4.8, ""completedGigIds"": [], ""currentLocationId"": ""loc_town_square"" } }";

            bool ok;
            try
            {
                PlayerCharacter p = RosterLoader.Load(validPlayer, "inline-player.json", locations).Player;
                log.Add($"  loaded player: {p.DisplayName}, money {p.Money}, karma {p.Karma}, notoriety {p.Notoriety}, gigRating {p.GigRating}, at {p.CurrentLocationId}");
                ok = Expect(log, p.DisplayName == "Test Player" && p.Money == 150 && p.Karma == 0.1f && p.Notoriety == 0f && p.GigRating == 4.8f
                    && p.InventoryItemIds.SequenceEqual(new[] { "item_phone" }) && p.CompletedGigIds.Count == 0 && p.CurrentLocationId == MvpTownData.TownSquare,
                    "control: a valid player section loads with every value intact");
            }
            catch (RosterLoadException ex)
            {
                ok = Expect(log, false, "control: a valid player section should load, but: " + ex.Message);
            }

            string brokenPlayer = Corrupt(Corrupt(Corrupt(validPlayer,
                ("\"gigRating\": 4.8", "\"gigRating\": 7.5")),
                ("\"loc_town_square\"", "\"loc_moon\"")),
                ("\"karma\": 0.1, ", ""));

            ok &= ExpectLoadFailure(log, "player with gigRating 7.5, an unknown location, and karma removed", brokenPlayer, locations,
                ("player.gigRating", "must be a number from 0 to 5"),
                ("player.currentLocationId", "unknown location id 'loc_moon'"),
                ("player.karma", "missing required field"));
            return ok;
        }

        private static void ReportScheduleVocabulary(List<string> log, string json, string rosterPath, LocationRegistry locations)
        {
            log.Add("");
            log.Add("=== R5. Roster schedule ActionType values (report, not pass/fail) ===");
            Roster roster = RosterLoader.Load(json, rosterPath, locations);

            var blocks = roster.Npcs.SelectMany(n => n.Schedule).ToList();
            log.Add($"  {blocks.Count} schedule blocks across {roster.Npcs.Count} NPCs have validated ActionType values.");
            log.Add("  Blocks at each employed NPC's own workplace, as the utility AI sees them:");

            foreach (var npc in roster.Npcs.Where(n => n.Job != null))
            {
                foreach (var block in npc.Schedule.Where(b => b.LocationId == npc.Job.WorkplaceId))
                {
                    string seenAs = block.ActionType.ToString();
                    log.Add($"    {npc.Id,-18} {block.Start:hh\\:mm}-{block.End:hh\\:mm}  \"{block.Activity}\" -> {seenAs}");
                }
            }
            log.Add("RESULT: REPORT ONLY");
        }

        private static bool VerifyRosaDay(List<string> log, string json, string rosterPath, LocationRegistry locations)
        {
            NPC rosa = RosterLoader.Load(json, rosterPath, locations).NpcsById["npc_rosa_shop"];
            GameTime start = GameTime.FromDateTime(rosa.LastSimulatedAt);

            log.Add($"  Rosa as loaded: {rosa.Job.Title} @ {rosa.Job.WorkplaceId}, Diligence {rosa.Personality.Diligence}, Performance {rosa.Job.Performance}, tier {rosa.CurrentTier}.");
            log.Add($"  Starts at her authored lastSimulatedAt, {rosa.LastSimulatedAt:yyyy-MM-dd HH:mm} UTC = {start}, with her authored needs. Seed 42, same as check 9.");
            log.Add("  Her schedule, and what the utility AI makes of each block:");
            foreach (var block in rosa.Schedule)
            {
                string seenAs = block.ActionType.ToString();
                log.Add($"    {block.Start:hh\\:mm}-{block.End:hh\\:mm}  {block.LocationId,-22} \"{block.Activity}\" -> {seenAs}");
            }
            return SimulationVerification.RunDayTimeline(log, rosa, start, 42, locations);
        }

        private static bool VerifyScheduleValidation(List<string> log, string json, LocationRegistry locations)
        {
            JObject Copy() => JObject.Parse(json);
            string Serialize(JObject root) => root.ToString();
            var clean = RosterLoader.Load(json, "control.json", locations);
            bool ok = Expect(log, clean.Npcs.Sum(n => n.Schedule.Count) == 134, "control: all 134 typed blocks load, including touching and midnight-wrapping blocks");
            const string actionPath = "npcs[0](npc_ana_diner).schedule[0].actionType";
            foreach (var value in new JToken[] { new JValue("Bogus"), new JValue("0"), new JValue(0), new JValue("eat"), new JValue("Eat, Sleep"), JValue.CreateNull() })
            {
                var bad = Copy(); bad["npcs"][0]["schedule"][0]["actionType"] = value;
                ok &= ExpectLoadFailure(log, "invalid actionType " + value.ToString(), Serialize(bad), locations,
                    (actionPath, "must be"));
            }
            var missing = Copy(); ((JObject)missing["npcs"][0]["schedule"][0]).Remove("actionType");
            ok &= ExpectLoadFailure(log, "missing actionType", Serialize(missing), locations, (actionPath, "missing required field"));

            var combined = Copy();
            combined["npcs"][0]["schedule"][0]["actionType"] = "Bogus";
            combined["npcs"][0]["schedule"][1]["start"] = "07:30";
            combined["npcs"][7]["relationships"]["npc_rosa_shop"]["type"] = "Friend";
            ok &= ExpectLoadFailure(log, "bogus ActionType + overlap + non-mutual Family", Serialize(combined), locations,
                (actionPath, "must be"),
                ("npcs[0](npc_ana_diner).schedule[1]", "overlaps npcs[0](npc_ana_diner).schedule[0]"),
                (RosaPath + ".relationships.npc_priya_florist.type", "non-mutual Family link"));

            foreach (string start in new[] { "06:50", "23:30" })
            {
                var bad = Copy(); bad["npcs"][0]["schedule"][0]["start"] = start;
                ok &= ExpectLoadFailure(log, "overlap across midnight: " + start, Serialize(bad), locations,
                    ("npcs[0](npc_ana_diner).schedule[7]", "overlaps npcs[0](npc_ana_diner).schedule[0]"));
            }
            foreach (string type in new[] { "Family", "Rival", "Romantic" })
            {
                var bad = Copy();
                var npcs = (JArray)bad["npcs"];
                JObject owner = npcs.OfType<JObject>().First(n => ((JObject)n["relationships"]).Properties().Any(r => (string)r.Value["type"] == type));
                JProperty link = ((JObject)owner["relationships"]).Properties().First(r => (string)r.Value["type"] == type);
                JObject target = npcs.OfType<JObject>().Single(n => (string)n["id"] == link.Name);
                ((JObject)target["relationships"]).Remove((string)owner["id"]);
                string path = $"npcs[{npcs.IndexOf(owner)}]({owner["id"]}).relationships.{link.Name}.type";
                ok &= ExpectLoadFailure(log, "missing reverse " + type, Serialize(bad), locations, (path, "non-mutual " + type + " link"));
            }
            // Negative work control: preserve narrative work labels, explicitly disable mechanical shifts.
            NPC rosa = RosterLoader.Load(json, "control.json", locations).NpcsById["npc_rosa_shop"];
            foreach (var block in rosa.Schedule.Where(b => b.ActionType == NpcAction.WorkShift)) block.ActionType = NpcAction.Idle;
            var controlLog = new List<string>();
            bool controlPassed = SimulationVerification.RunDayTimeline(controlLog, rosa, GameTime.FromDateTime(rosa.LastSimulatedAt), 42, locations);
            string workLine = controlLog.Single(l => l.Contains("employed as"));
            log.Add("  Negative work control: " + workLine);
            ok &= Expect(log, !controlPassed && workLine.Contains("worked 0 of 0"), "same work-hours check fails with shifts disabled despite unchanged flavor labels");
            return ok;
        }

        private static bool ExpectLoadFailure(List<string> log, string title, string json, LocationRegistry locations,
            params (string Path, string Fragment)[] expected)
        {
            log.Add($"  --- {title}");
            try
            {
                Roster roster = RosterLoader.Load(json, "corrupted-copy.json", locations);
                return Expect(log, false, $"loaded without any error and returned {roster.Npcs.Count} NPCs, a silent acceptance");
            }
            catch (RosterLoadException ex)
            {
                foreach (var error in ex.Errors) log.Add("    error: " + error);
                bool ok = Expect(log, ex.Errors.Count == expected.Length,
                    $"threw RosterLoadException with exactly {expected.Length} error(s) (got {ex.Errors.Count})");
                foreach (var (path, fragment) in expected)
                {
                    ok &= Expect(log, ex.Errors.Any(e => e.Path == path && e.Message.Contains(fragment)),
                        $"an error at {path} saying \"{fragment}\"");
                }
                return ok;
            }
            catch (Exception ex)
            {
                return Expect(log, false, $"threw a generic {ex.GetType().Name} instead of RosterLoadException: {ex.Message}");
            }
        }

        // A target that doesn't match exactly once would make the check pass or fail for the wrong reason.
        private static string Corrupt(string json, (string Find, string Replace) corruption)
        {
            int count = 0;
            for (int i = json.IndexOf(corruption.Find, StringComparison.Ordinal); i >= 0; i = json.IndexOf(corruption.Find, i + 1, StringComparison.Ordinal))
                count++;
            if (count != 1)
                throw new InvalidOperationException($"Corruption target must occur exactly once, found {count}: {corruption.Find}");
            return json.Replace(corruption.Find, corruption.Replace);
        }
    }
}
