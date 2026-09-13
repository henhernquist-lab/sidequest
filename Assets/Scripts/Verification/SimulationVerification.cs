using System;
using System.Collections.Generic;
using System.Linq;
using SideQuest.Core;
using SideQuest.Data;
using SideQuest.Data.Verification;
using SideQuest.World;

namespace SideQuest.Simulation.Verification
{
    // Headless checks for the clock, locations, and utility AI. Each behavioral claim is paired
    // with a control that has to come out the other way (AGENTS.md verification discipline).
    public static class SimulationVerification
    {
        private sealed class CountingRandom : Random
        {
            public int Draws { get; private set; }

            public CountingRandom(int seed) : base(seed) { }

            public override double NextDouble()
            {
                Draws++;
                return base.NextDouble();
            }
        }

        public static bool RunAll(List<string> log)
        {
            bool ok = true;
            ok &= Section(log, "0. Test NPC fixture is field-complete (add-npc rule)", VerifyFixtureComplete);
            ok &= Section(log, "1. Game clock: day rollover", VerifyClock);
            ok &= Section(log, "2. Locations: open/closed lookup", VerifyLocations);
            ok &= Section(log, "3. Starving NPC abandons shift; Hunger-80 control keeps working", VerifyStarvingAbandonsShift);
            ok &= Section(log, "4. Extrovert vs introvert diverge; identical-personality control does not", VerifyPersonalityDivergence);
            ok &= Section(log, "5. Feasibility-zero action never picked; feasible controls are picked", VerifyFeasibilityZero);
            ok &= Section(log, "6. LOD: only Active runs a scoring pass", VerifyLod);
            ok &= Section(log, "7. Typo'd schedule activity is caught, not silent", VerifyUnmappedActivity);
            ok &= Section(log, "8. Scheduled activity happens at the scheduled location", VerifyScheduledVenue);
            ok &= Section(log, "9. One full in-game day, single NPC", VerifyFullDay);
            return ok;
        }

        private static bool VerifyScheduledVenue(List<string> log)
        {
            var feasibility = new FeasibilityEvaluator(MvpTownData.CreateRegistry());

            string SocializeVenueAt(int hour)
            {
                var npc = MakeBarista("npc_venue_test");
                npc.CurrentLocationId = MvpTownData.ApartmentComplex;
                return feasibility.Evaluate(npc, NpcAction.Socialize, GameTime.At(1, hour)).VenueId;
            }

            string duringLeisure = SocializeVenueAt(20);
            string outsideAnyBlock = SocializeVenueAt(17);
            log.Add($"  Barista at home; her Leisure block 19:00-22:00 names {MvpTownData.TownSquare}.");
            log.Add($"  Socialize venue at 20:00 (inside Leisure block): {duringLeisure}");
            log.Add($"  Socialize venue at 17:00 (no block)            : {outsideAnyBlock}");

            bool ok = Expect(log, duringLeisure == MvpTownData.TownSquare, "inside the block, Socialize goes to the block's location");
            ok &= Expect(log, outsideAnyBlock != MvpTownData.TownSquare,
                "control: with no block, the same NPC lands somewhere else, so the block's location is what moved her");
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

        private static TimeSpan H(int hour) => TimeSpan.FromHours(hour);

        private static string NeedsText(Needs n) =>
            $"H{n.Hunger,4:0} E{n.Energy,4:0} S{n.Social,4:0} Hy{n.Hygiene,4:0}";

        private static UtilityScorer Scorer(LocationRegistry registry, Random random) =>
            new UtilityScorer(new FeasibilityEvaluator(registry), random);

        // Every §7 field populated, per the add-npc skill.
        private static NPC MakeBarista(string id, float extroversion = 0.5f, float diligence = 0.6f, float ambition = 0.5f)
        {
            return new NPC
            {
                Id = id,
                DisplayName = id,
                Age = 29,
                AppearanceRef = "skin_test_default",
                Personality = new Personality
                {
                    Ambition = ambition,
                    Extroversion = extroversion,
                    Honesty = 0.5f,
                    RiskTolerance = 0.5f,
                    Warmth = 0.5f,
                    Diligence = diligence
                },
                Needs = new Needs { Hunger = 80f, Energy = 80f, Social = 80f, Hygiene = 80f },
                Job = new Job { Title = "Barista", WorkplaceId = MvpTownData.CornerDiner, Income = 320, Performance = 0.7f },
                HomeLocationId = MvpTownData.ApartmentComplex,
                InventoryItemIds = new List<string> { "item_house_key" },
                Money = 400,
                Schedule = new List<ScheduleBlock>
                {
                    new ScheduleBlock { Start = H(23), End = H(7), Activity = "Sleep", LocationId = MvpTownData.ApartmentComplex },
                    new ScheduleBlock { Start = H(7), End = H(15), Activity = "Work", LocationId = MvpTownData.CornerDiner },
                    new ScheduleBlock { Start = H(19), End = H(22), Activity = "Leisure", LocationId = MvpTownData.TownSquare },
                },
                Goals = new Goals
                {
                    ShortTerm = new List<string> { "Save for a used car" },
                    LongTerm = new List<string> { "Open a cafe" }
                },
                Relationships = new Dictionary<string, Relationship>(),
                MemoryStream = new List<MemoryFact>(),
                CurrentTier = SimTier.Active,
                LastSimulatedAt = GameTime.At(1, 0).ToDateTime(),
                CurrentLocationId = MvpTownData.ApartmentComplex,
                CurrentActivity = NpcAction.Idle.ToString()
            };
        }

        private static bool VerifyFixtureComplete(List<string> log)
        {
            var result = NpcDataModelVerification.CheckAllReferenceFieldsPopulated(MakeBarista("npc_fixture"), nameof(NPC));
            return Expect(log, result.Passed,
                result.Passed ? "every reference field populated" : "missing: " + string.Join(", ", result.MissingFields));
        }

        private static bool VerifyClock(List<string> log)
        {
            var clock = new GameClock(GameTime.At(3, 23, 58));
            var events = new List<string>();
            clock.DayStarted += day => events.Add($"DayStarted({day})");
            clock.Ticked += now => events.Add($"Ticked({now})");

            log.Add($"  start           : {clock.Now}");
            clock.AdvanceGameTime(TimeSpan.FromMinutes(1));
            log.Add($"  +1 min          : {clock.Now}   events: {string.Join(", ", events)}");
            bool ok = Expect(log, clock.Now == GameTime.At(3, 23, 59), "23:59 is still Day 3");
            ok &= Expect(log, !events.Any(e => e.StartsWith("DayStarted")), "no DayStarted before midnight");

            events.Clear();
            clock.AdvanceGameTime(TimeSpan.FromMinutes(1));
            log.Add($"  +1 min          : {clock.Now}   events: {string.Join(", ", events)}");
            ok &= Expect(log, clock.Now == GameTime.At(4, 0, 0), "rolls over to Day 4 00:00");
            ok &= Expect(log, events.SequenceEqual(new[] { "DayStarted(4)", "Ticked(Day 4 00:00)" }),
                "DayStarted(4) fires exactly once, before Ticked");

            events.Clear();
            clock.AdvanceGameTime(TimeSpan.FromHours(50));
            log.Add($"  +50 h           : {clock.Now}   events: {string.Join(", ", events)}");
            ok &= Expect(log, clock.Now == GameTime.At(6, 2, 0), "Day 4 00:00 + 50h = Day 6 02:00");
            ok &= Expect(log, events.SequenceEqual(new[] { "DayStarted(5)", "DayStarted(6)", "Ticked(Day 6 02:00)" }),
                "one DayStarted per day boundary crossed");

            var ratioClock = new GameClock(GameTime.At(1, 0));
            ratioClock.AdvanceRealSeconds(120);
            log.Add($"  ratio           : {SimulationTuning.GameMinutesPerRealSecond} game-min per real-sec; Day 1 00:00 + 120 real s = {ratioClock.Now}");
            ok &= Expect(log, ratioClock.Now == GameTime.At(2, 0), "120 real seconds is exactly one in-game day at this ratio");

            var sample = GameTime.At(4, 13, 37);
            var roundTrip = GameTime.FromDateTime(sample.ToDateTime());
            log.Add($"  DateTime bridge : {sample} -> {sample.ToDateTime():yyyy-MM-dd HH:mm} -> {roundTrip}");
            ok &= Expect(log, roundTrip == sample, "GameTime -> DateTime -> GameTime round-trips exactly");
            return ok;
        }

        private static bool VerifyLocations(List<string> log)
        {
            var registry = MvpTownData.CreateRegistry();
            foreach (var l in registry.All.OrderBy(l => l.Id, StringComparer.Ordinal))
            {
                log.Add($"  {l.Id,-22} {l.DisplayName,-18} {l.Hours,-12} workplace={l.IsWorkplace,-5} food={l.ServesFood,-5} social={l.IsSocialVenue,-5} residence={l.IsResidence}");
            }

            bool ok = Expect(log, registry.All.Count == 6, "6 MVP locations loaded from data");

            bool Check(string id, int hour, int minute, bool expectOpen)
            {
                bool open = registry.IsOpenAt(id, new TimeSpan(hour, minute, 0));
                return Expect(log, open == expectOpen, $"{id} at {hour:00}:{minute:00} -> {(open ? "OPEN" : "CLOSED")}");
            }

            ok &= Check(MvpTownData.CornerDiner, 12, 0, true);
            ok &= Check(MvpTownData.CornerDiner, 23, 0, false);
            ok &= Check(MvpTownData.CornerDiner, 5, 59, false);
            ok &= Check(MvpTownData.CornerDiner, 6, 0, true);
            ok &= Check(MvpTownData.CornerDiner, 22, 0, false);
            ok &= Check(MvpTownData.AutoShop, 3, 0, false);
            ok &= Check(MvpTownData.TownSquare, 3, 0, true);
            ok &= Check(MvpTownData.ApartmentComplex, 3, 0, true);
            ok &= Check("loc_does_not_exist", 12, 0, false);

            var overnight = OpeningHours.Between(18, 2);
            ok &= Expect(log, overnight.IsOpenAt(H(1)) && overnight.IsOpenAt(H(19)) && !overnight.IsOpenAt(H(3)) && !overnight.IsOpenAt(H(17)),
                "overnight hours 18:00-02:00: open at 01:00 and 19:00, closed at 03:00 and 17:00");
            return ok;
        }

        private static bool VerifyStarvingAbandonsShift(List<string> log)
        {
            var registry = MvpTownData.CreateRegistry();
            var midShift = GameTime.At(1, 10);
            const int trials = 300;

            var reference = MakeBarista("npc_reference");
            float shiftScore = SimulationTuning.ScheduleBaselineUrgency * UtilityScorer.PersonalityWeight(reference.Personality, NpcAction.WorkShift);
            float eatWeight = UtilityScorer.PersonalityWeight(reference.Personality, NpcAction.Eat);
            float crossoverHunger = 100f * (1f - MathF.Pow(shiftScore / eatWeight, 1f / SimulationTuning.UrgencyCurveExponent));
            log.Add($"  Barista, Diligence {reference.Personality.Diligence}, at {midShift} (inside the 07:00-15:00 Work block)");
            log.Add($"  Noise-free, Eat out-scores the shift once Hunger drops below {crossoverHunger:0.0}");

            int CountChoices(float hunger, NpcAction action, bool printBreakdown)
            {
                int count = 0;
                for (int seed = 0; seed < trials; seed++)
                {
                    var npc = MakeBarista("npc_shift_test");
                    npc.Needs = new Needs { Hunger = hunger, Energy = 80f, Social = 80f, Hygiene = 80f };
                    npc.CurrentLocationId = MvpTownData.CornerDiner;
                    var scores = Scorer(registry, new Random(seed)).ScoreAll(npc, midShift);
                    if (printBreakdown && seed == 0)
                    {
                        log.Add($"  Score breakdown at Hunger {hunger} (seed 0):");
                        foreach (var s in scores.OrderByDescending(s => s.Final)) log.Add("    " + s);
                    }
                    if (UtilityScorer.Best(scores).Action == action) count++;
                }
                return count;
            }

            int starvingAte = CountChoices(10f, NpcAction.Eat, true);
            int controlWorked = CountChoices(80f, NpcAction.WorkShift, true);
            bool ok = Expect(log, starvingAte == trials, $"Hunger 10: chose Eat over the shift in {starvingAte}/{trials} seeded runs");
            ok &= Expect(log, controlWorked == trials, $"control, Hunger 80: stayed on shift in {controlWorked}/{trials} seeded runs");

            log.Add($"  Informational sweep near the crossover (Eat rate during the shift, {trials} seeds each):");
            foreach (float hunger in new[] { 15f, 22f, 25f, 27f, 29f, 32f, 40f })
            {
                int ate = CountChoices(hunger, NpcAction.Eat, false);
                log.Add($"    Hunger {hunger,3:0}: Eat {ate * 100.0 / trials,5:0.0}%");
            }
            return ok;
        }

        private static bool VerifyPersonalityDivergence(List<string> log)
        {
            var registry = MvpTownData.CreateRegistry();
            var offShift = GameTime.At(1, 17);
            const int trials = 2000;

            Dictionary<NpcAction, int> Distribution(float extroversion, int seedBase)
            {
                var counts = new Dictionary<NpcAction, int>();
                for (int i = 0; i < trials; i++)
                {
                    var npc = MakeBarista("npc_personality_test", extroversion: extroversion);
                    npc.Needs = new Needs { Hunger = 70f, Energy = 70f, Social = 45f, Hygiene = 70f };
                    npc.CurrentLocationId = MvpTownData.TownSquare;
                    var action = Scorer(registry, new Random(seedBase + i)).Choose(npc, offShift).Action;
                    counts[action] = counts.TryGetValue(action, out int c) ? c + 1 : 1;
                }
                return counts;
            }

            string Describe(Dictionary<NpcAction, int> d) =>
                string.Join(", ", d.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value * 100.0 / trials:0.0}%"));

            double SocializeRate(Dictionary<NpcAction, int> d) =>
                d.TryGetValue(NpcAction.Socialize, out int c) ? (double)c / trials : 0.0;

            log.Add($"  Identical state at {offShift} (no scheduled block): Hunger 70, Energy 70, Social 45, Hygiene 70; {trials} seeded runs each");

            var extrovert = Distribution(0.95f, 0);
            var introvert = Distribution(0.05f, 100_000);
            log.Add($"  Extroversion 0.95: {Describe(extrovert)}");
            log.Add($"  Extroversion 0.05: {Describe(introvert)}");
            double treatmentGap = Math.Abs(SocializeRate(extrovert) - SocializeRate(introvert));
            bool ok = Expect(log, treatmentGap >= 0.30, $"Socialize rate differs by {treatmentGap * 100:0.0} points between extrovert and introvert");

            // 0.22 sits near a Socialize/PursueGoal tie, so noise genuinely decides. Two NPCs with that
            // same personality should land on the same split; a gap here would mean the harness is lying.
            var controlA = Distribution(0.22f, 200_000);
            var controlB = Distribution(0.22f, 300_000);
            log.Add($"  Control A, Extroversion 0.22: {Describe(controlA)}");
            log.Add($"  Control B, Extroversion 0.22: {Describe(controlB)}");
            double controlGap = Math.Abs(SocializeRate(controlA) - SocializeRate(controlB));
            ok &= Expect(log, controlGap < 0.06, $"control: identical personalities differ by only {controlGap * 100:0.0} points");
            ok &= Expect(log, SocializeRate(controlA) > 0.05 && SocializeRate(controlA) < 0.95,
                "control case is genuinely mixed, so the noise is doing real work rather than one action always winning");
            return ok;
        }

        private static bool VerifyFeasibilityZero(List<string> log)
        {
            var registry = MvpTownData.CreateRegistry();
            const int trials = 500;

            NPC Mechanic(int workStart, int workEnd)
            {
                var npc = MakeBarista("npc_mechanic_test", diligence: 0.9f);
                npc.Job = new Job { Title = "Mechanic", WorkplaceId = MvpTownData.AutoShop, Income = 400, Performance = 0.7f };
                npc.Schedule = new List<ScheduleBlock>
                {
                    new ScheduleBlock { Start = H(workStart), End = H(workEnd), Activity = "Work", LocationId = MvpTownData.AutoShop }
                };
                npc.Needs = new Needs { Hunger = 95f, Energy = 95f, Social = 95f, Hygiene = 95f };
                npc.CurrentLocationId = MvpTownData.AutoShop;
                return npc;
            }

            NPC StarvingWithStaleHome()
            {
                var npc = MakeBarista("npc_stale_home_test");
                npc.HomeLocationId = "loc_demolished_house";
                npc.Job = null;
                npc.Schedule = new List<ScheduleBlock>();
                npc.Needs = new Needs { Hunger = 2f, Energy = 95f, Social = 95f, Hygiene = 95f };
                npc.CurrentLocationId = MvpTownData.TownSquare;
                return npc;
            }

            bool ok = RunFeasibilityCase(log, registry, trials, "A: Mechanic scheduled 00:00-08:00, Auto Shop open 08:00-18:00",
                () => Mechanic(0, 8), GameTime.At(1, 3), NpcAction.WorkShift, expectPicked: false);
            ok &= RunFeasibilityCase(log, registry, trials, "A control: same Mechanic scheduled 08:00-16:00",
                () => Mechanic(8, 16), GameTime.At(1, 10), NpcAction.WorkShift, expectPicked: true);
            ok &= RunFeasibilityCase(log, registry, trials, "B: Hunger 2, home id not in registry, Diner and Store closed",
                StarvingWithStaleHome, GameTime.At(1, 3), NpcAction.Eat, expectPicked: false);
            ok &= RunFeasibilityCase(log, registry, trials, "B control: same NPC while the Diner is open",
                StarvingWithStaleHome, GameTime.At(1, 12), NpcAction.Eat, expectPicked: true);
            return ok;
        }

        private static bool RunFeasibilityCase(List<string> log, LocationRegistry registry, int trials, string title,
            Func<NPC> makeNpc, GameTime now, NpcAction target, bool expectPicked)
        {
            log.Add($"  --- {title}, at {now}");
            int picked = 0;
            bool finalAlwaysZero = true;
            bool wouldOtherwiseWin = true;

            for (int seed = 0; seed < trials; seed++)
            {
                var scores = Scorer(registry, new Random(seed)).ScoreAll(makeNpc(), now);
                if (seed == 0)
                {
                    foreach (var s in scores.OrderByDescending(s => s.Final)) log.Add("    " + s);
                }
                if (UtilityScorer.Best(scores).Action == target) picked++;

                var targetScore = scores.First(s => s.Action == target);
                finalAlwaysZero &= targetScore.Final == 0f;
                float targetIgnoringFeasibility = targetScore.Urgency * targetScore.PersonalityWeight;
                wouldOtherwiseWin &= scores.Where(s => s.Action != target).All(s => targetIgnoringFeasibility > s.Final);
            }

            if (expectPicked)
            {
                return Expect(log, picked == trials, $"{target} picked in {picked}/{trials} seeded runs");
            }

            bool ok = Expect(log, picked == 0, $"{target} picked in {picked}/{trials} seeded runs");
            ok &= Expect(log, finalAlwaysZero, $"{target} final score was exactly 0 in every run");
            ok &= Expect(log, wouldOtherwiseWin, $"{target}'s urgency x weight beat every other action's final score in every run, so only feasibility stopped it");
            return ok;
        }

        private static bool VerifyLod(List<string> log)
        {
            var registry = MvpTownData.CreateRegistry();
            var start = GameTime.At(1, 10);
            var oneHour = TimeSpan.FromHours(1);
            float floor = SimulationTuning.OffscreenSubsistenceFloor;

            var activeRandom = new CountingRandom(1);
            var active = MakeBarista("npc_active");
            active.CurrentTier = SimTier.Active;
            var activeDecision = new NpcSimulator(Scorer(registry, activeRandom)).Step(active, start, oneHour);
            log.Add($"  Active     : decision={activeDecision?.Chosen.Action.ToString() ?? "none"}, RNG draws={activeRandom.Draws}, needs now {NeedsText(active.Needs)}");
            bool ok = Expect(log, activeDecision != null && activeRandom.Draws == 7, "Active step ran a full scoring pass (7 actions scored)");

            var backgroundRandom = new CountingRandom(2);
            var background = MakeBarista("npc_background");
            background.CurrentTier = SimTier.Background;
            var backgroundDecision = new NpcSimulator(Scorer(registry, backgroundRandom)).Step(background, start, oneHour);
            log.Add($"  Background : decision={backgroundDecision?.Chosen.Action.ToString() ?? "none"}, RNG draws={backgroundRandom.Draws}, activity={background.CurrentActivity}, needs now {NeedsText(background.Needs)}");
            ok &= Expect(log, backgroundDecision == null && backgroundRandom.Draws == 0, "Background step ran no scoring pass");
            ok &= Expect(log, background.Needs.Hunger == 80f - SimulationTuning.HungerDecayPerHour, "Background needs still decayed");
            ok &= Expect(log, background.CurrentActivity == NpcAction.WorkShift.ToString(), "Background activity came straight from the schedule (Work block at 11:00)");

            var dormantRandom = new CountingRandom(3);
            var dormantSim = new NpcSimulator(Scorer(registry, dormantRandom));
            var dormant = MakeBarista("npc_dormant");
            dormant.CurrentTier = SimTier.Dormant;
            dormant.LastSimulatedAt = start.ToDateTime();
            var later = start.Plus(TimeSpan.FromHours(48));

            var dormantDecision = dormantSim.Step(dormant, later, oneHour);
            ok &= Expect(log, dormantDecision == null && dormantRandom.Draws == 0 && dormant.Needs.Hunger == 80f
                && dormant.LastSimulatedAt == start.ToDateTime(), "Dormant Step changed nothing");

            dormantSim.CatchUp(dormant, later);
            log.Add($"  Dormant catch-up after 48h: RNG draws={dormantRandom.Draws}, activity={dormant.CurrentActivity}, needs now {NeedsText(dormant.Needs)}, last simulated {GameTime.FromDateTime(dormant.LastSimulatedAt)}");
            ok &= Expect(log, dormantRandom.Draws == 0, "catch-up ran no scoring pass");
            ok &= Expect(log, dormant.Needs.Hunger == floor && dormant.Needs.Energy == floor && dormant.Needs.Social == floor && dormant.Needs.Hygiene == floor,
                $"48h of decay resolved in one batch, clamped at the off-screen floor ({floor})");
            ok &= Expect(log, GameTime.FromDateTime(dormant.LastSimulatedAt) == later, "LastSimulatedAt advanced to the catch-up time");

            // Control: a short absence decays without reaching the floor, so the floor isn't just overwriting needs.
            var shortAbsence = MakeBarista("npc_short_absence");
            shortAbsence.CurrentTier = SimTier.Dormant;
            shortAbsence.LastSimulatedAt = start.ToDateTime();
            dormantSim.CatchUp(shortAbsence, start.Plus(TimeSpan.FromHours(2)));
            float expectedHunger = 80f - SimulationTuning.HungerDecayPerHour * 2f;
            log.Add($"  Control, 2h catch-up: needs now {NeedsText(shortAbsence.Needs)}");
            ok &= Expect(log, shortAbsence.Needs.Hunger == expectedHunger && expectedHunger > floor,
                $"control: 2h catch-up decayed Hunger to {expectedHunger} without clamping");
            return ok;
        }

        private static bool VerifyUnmappedActivity(List<string> log)
        {
            var clean = MakeBarista("npc_clean_schedule");
            var typo = MakeBarista("npc_typo_schedule");
            typo.Schedule.Add(new ScheduleBlock { Start = H(15), End = H(17), Activity = "Wrok", LocationId = MvpTownData.CornerDiner });

            var cleanUnmapped = ScheduleService.FindUnmappedActivities(clean);
            var typoUnmapped = ScheduleService.FindUnmappedActivities(typo);
            log.Add($"  clean schedule, unmapped: [{string.Join(", ", cleanUnmapped)}]");
            log.Add($"  typo schedule, unmapped : [{string.Join(", ", typoUnmapped)}]");
            log.Add($"  what the scorer sees at 16:00 for the typo'd block: {ScheduleService.ScheduledActionAt(typo, H(16))?.ToString() ?? "no scheduled action (the silent failure this lint exists to catch)"}");

            bool ok = Expect(log, cleanUnmapped.Count == 0, "control: clean schedule reports nothing");
            ok &= Expect(log, typoUnmapped.SequenceEqual(new[] { "Wrok" }), "typo'd 'Wrok' is reported");
            return ok;
        }

        private static bool VerifyFullDay(List<string> log)
        {
            var mara = MakeBarista("npc_mara", extroversion: 0.6f, diligence: 0.6f, ambition: 0.5f);
            mara.DisplayName = "Mara Whitfield";
            mara.Needs = new Needs { Hunger = 70f, Energy = 25f, Social = 70f, Hygiene = 80f };

            log.Add("  Mara, Barista. Schedule: Sleep 23-07 (home), Work 07-15 (Corner Diner), Leisure 19-22 (Town Square). Seed 42.");
            return RunDayTimeline(log, mara, GameTime.At(1, 0), 42, MvpTownData.CreateRegistry());
        }

        // Steps one Active NPC hourly for 24 in-game hours, printing the timeline and plausibility guardrails.
        public static bool RunDayTimeline(List<string> log, NPC npc, GameTime start, int seed, LocationRegistry registry)
        {
            if (npc.CurrentTier != SimTier.Active)
                return Expect(log, false, $"{npc.Id} is {npc.CurrentTier}; only Active NPCs get a scoring pass to print");

            var sim = new NpcSimulator(Scorer(registry, new Random(seed)));

            log.Add("  Needs are the state the NPC decided on, before that hour played out.");
            log.Add("  time   action     venue                   needs                     scheduled   winner vs runner-up");

            var time = start;
            var hoursPerAction = new Dictionary<NpcAction, int>();
            int scheduledWorkHours = 0;
            int scheduledWorkHoursWorked = 0;
            float lowestNeed = 100f;

            for (int hour = 0; hour < 24; hour++)
            {
                string needs = NeedsText(npc.Needs);
                NpcAction? scheduled = ScheduleService.ScheduledActionAt(npc, time.TimeOfDay);
                var decision = sim.Step(npc, time, TimeSpan.FromHours(1));
                var runnerUp = decision.AllScores.Where(s => s != decision.Chosen).OrderByDescending(s => s.Final).First();

                log.Add($"  {time.TimeOfDay:hh\\:mm}  {decision.Chosen.Action,-10} {npc.CurrentLocationId,-23} {needs}  {scheduled?.ToString() ?? "-",-10}  " +
                        $"{decision.Chosen.Final:0.000} vs {runnerUp.Action} {runnerUp.Final:0.000}");

                hoursPerAction[decision.Chosen.Action] = hoursPerAction.TryGetValue(decision.Chosen.Action, out int n) ? n + 1 : 1;
                if (scheduled == NpcAction.WorkShift)
                {
                    scheduledWorkHours++;
                    if (decision.Chosen.Action == NpcAction.WorkShift) scheduledWorkHoursWorked++;
                }
                lowestNeed = Math.Min(lowestNeed, Math.Min(Math.Min(npc.Needs.Hunger, npc.Needs.Energy), Math.Min(npc.Needs.Social, npc.Needs.Hygiene)));
                time = time.Plus(TimeSpan.FromHours(1));
            }

            int Hours(NpcAction action) => hoursPerAction.TryGetValue(action, out int v) ? v : 0;
            GameTime expectedEnd = start.Plus(TimeSpan.FromHours(24));

            log.Add($"  ended at {time}; hours per action: {string.Join(", ", hoursPerAction.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value}"))}");
            log.Add("  Plausibility guardrails (thresholds I picked, not ground truth; the timeline above is the real evidence):");
            bool ok = Expect(log, Hours(NpcAction.Sleep) >= 6, $"slept {Hours(NpcAction.Sleep)}h (>= 6)");
            ok &= Expect(log, Hours(NpcAction.Eat) >= 2, $"spent {Hours(NpcAction.Eat)}h eating (>= 2)");
            if (npc.Job != null)
            {
                ok &= Expect(log, scheduledWorkHours > 0 && scheduledWorkHoursWorked * 4 >= scheduledWorkHours * 3,
                    $"employed as {npc.Job.Title}: worked {scheduledWorkHoursWorked} of {scheduledWorkHours} hours the AI saw as scheduled work (needs > 0 scheduled and >= 75% worked)");
            }
            else
            {
                log.Add("  [ -- ] unemployed, so the work guardrail doesn't apply");
            }
            ok &= Expect(log, hoursPerAction.Count >= 4, $"used {hoursPerAction.Count} distinct actions (>= 4, not one action on repeat)");
            ok &= Expect(log, lowestNeed > 0f, $"lowest need all day was {lowestNeed:0.0} (never bottomed out)");
            ok &= Expect(log, time == expectedEnd, $"24 hourly steps from {start} end exactly at {expectedEnd}");
            return ok;
        }
    }
}
