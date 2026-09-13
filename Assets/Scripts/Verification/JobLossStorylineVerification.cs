using System;
using System.Collections.Generic;
using System.Linq;
using SideQuest.Core;
using SideQuest.Data;
using SideQuest.Loading;
using SideQuest.Simulation;
using SideQuest.Storylines;
using SideQuest.World;

namespace SideQuest.Verification
{
    public static class JobLossStorylineVerification
    {
        // Explicit test-world ownership; not inferred from the employee's traits or authored as roster fact.
        private static Dictionary<string, string> Employers() => new Dictionary<string, string>
        {
            [MvpTownData.GeneralStore] = "npc_greta_retired",
            [MvpTownData.AutoShop] = "npc_lenny_retired",
            [MvpTownData.CornerDiner] = "npc_iris_baker",
            [MvpTownData.PoliceStation] = "npc_petra_retired"
        };

        private sealed class Arc
        {
            public int Seed;
            public Roster Roster;
            public FoodCart Cart;
            public int FiredDay;
            public int LastDay;
            public bool SalKeptJob = true;
            public bool IncomeStopped = true;
            public readonly List<string> Timeline = new List<string>();
        }

        private static Arc RunArc(string path, int seed)
        {
            var locations = MvpTownData.CreateRegistry();
            var roster = RosterLoader.LoadFile(path, locations);
            var rosa = roster.NpcsById["npc_rosa_shop"];
            var sal = roster.NpcsById["npc_sal_mechanic"];
            var arc = new Arc { Seed = seed, Roster = roster };
            var clock = new GameClock(GameTime.FromDateTime(rosa.LastSimulatedAt));
            using var simulation = new StorylineSimulation(clock, roster.Npcs, locations, Employers(), seed);
            int startDay = clock.Now.Day;
            for (int i = 0; i < 100; i++)
            {
                simulation.Advance(TimeSpan.FromDays(1) - clock.Now.TimeOfDay);
                var progress = simulation.Storyline.Progress[rosa.Id];
                if (progress.LostJobDay.HasValue && arc.FiredDay == 0) arc.FiredDay = progress.LostJobDay.Value;
                arc.SalKeptJob &= sal.Job != null && sal.Job.Title == "Mechanic";
                if (rosa.Job == null && progress.LostJobDay < clock.Now.Day)
                    arc.IncomeStopped &= progress.IncomePaidToday == 0;
                simulation.Storyline.Carts.TryGetValue(rosa.Id, out var cart);
                arc.Cart = cart;
                arc.LastDay = clock.Now.Day;
                var facts = rosa.MemoryStream.Where(m => m.Timestamp == clock.Now.ToDateTime()
                    && (m.EventType == "JobLost" || m.EventType.StartsWith("Business", StringComparison.Ordinal))).ToList();
                arc.Timeline.Add($"day +{clock.Now.Day - startDay,2} ({clock.Now.Day}): job={rosa.Job?.Title ?? "unemployed"}; performance={rosa.Job?.Performance.ToString("0.000") ?? "-"}; poorDays={progress.ConsecutivePoorDays}; fireChance={progress.LastFiringChance:0.000}; wages={progress.IncomePaidToday}; savings={rosa.Money}; visitors={simulation.Storyline.LastFootTraffic}; cart={cart?.Health.ToString("0.000") ?? "-"} {cart?.Outcome.ToString() ?? ""}; facts={string.Join(",", facts.Select(f => f.EventType))}");
                if (cart != null && cart.Outcome != FoodCartOutcome.Operating) break;
            }
            return arc;
        }

        public static bool RunAll(List<string> log, string path)
        {
            bool ok = true;
            void Check(bool condition, string message) { log.Add($"[{(condition ? "PASS" : "FAIL")}] {message}"); ok &= condition; }
            log.Add("=== Job loss -> food cart: full simulation, authored roster, seeded decisions ===");
            log.Add("Test ownership map (scenario configuration): Greta/Store, Lenny/Auto Shop, Iris/Diner, Petra/Police. No roster traits changed.");
            Arc success = RunArc(path, 0);
            Arc failure = RunArc(path, 6);
            Check(success.Cart?.Outcome == FoodCartOutcome.Hired && failure.Cart?.Outcome == FoodCartOutcome.Closed, "same authored world, different seeds reach BOTH hire and closure within 100 days");
            foreach (var arc in new[] { success, failure }.Where(a => a != null))
            {
                log.Add($"--- SEED {arc.Seed}: {arc.Cart.Outcome} ---");
                log.AddRange(arc.Timeline);
                var rosa = arc.Roster.NpcsById["npc_rosa_shop"];
                Check(arc.FiredDay >= 261 && arc.FiredDay <= 270, $"Rosa fired after >=5 full poor-performance days, within 15 days: day {arc.FiredDay}");
                Check(arc.SalKeptJob, $"CONTROL: Sal remains Mechanic through day {arc.LastDay}, performance {arc.Roster.NpcsById["npc_sal_mechanic"].Job?.Performance:0.000}");
                Check(arc.IncomeStopped, "no wages during complete unemployed days");
                Check(arc.Cart.OpenedDay - arc.FiredDay >= StorylineTuning.BusinessWaitDays, "cart waits at least three days after job loss");
                var expected = new Dictionary<string, string[]>
                {
                    ["JobLost"] = new[] { rosa.Id, "npc_greta_retired" },
                    ["BusinessOpened"] = new[] { rosa.Id },
                    [arc.Cart.Outcome == FoodCartOutcome.Hired ? "BusinessHired" : "BusinessFailed"] = new[]
                        { rosa.Id, arc.Cart.EmployeeId ?? "npc_priya_florist" }
                };
                foreach (var pair in expected)
                {
                    var facts = rosa.MemoryStream.Where(m => m.EventType == pair.Key).ToList();
                    Check(facts.Count == 1 && facts[0].InvolvedNpcIds.OrderBy(id => id).SequenceEqual(pair.Value.OrderBy(id => id)),
                        $"memory {pair.Key}: exactly once, ids=[{string.Join(",", facts.FirstOrDefault()?.InvolvedNpcIds ?? new List<string>())}]");
                    Check(arc.Roster.NpcsById["npc_priya_florist"].MemoryStream.Any(m => m.EventType == pair.Key),
                        $"close Family Priya actually received {pair.Key}");
                }
                Check(arc.Roster.Npcs.SelectMany(n => n.MemoryStream).Any(m => m.EventType.StartsWith("Shared:", StringComparison.Ordinal)),
                    "canonical storyline facts propagate through actual Socialize decisions");
                if (arc.Cart.Outcome == FoodCartOutcome.Hired)
                {
                    var hire = arc.Roster.NpcsById[arc.Cart.EmployeeId];
                    Check(hire.Job != null && hire.Job.WorkplaceId == MvpTownData.TownSquare
                        && hire.Schedule.Any(b => b.ActionType == NpcAction.WorkShift), $"real new Job and shift for {hire.Id}: {hire.Job?.Title}");
                    Check(new FeasibilityEvaluator(MvpTownData.CreateRegistry()).Evaluate(hire, NpcAction.WorkShift, GameTime.At(arc.LastDay, 12)).Value == 1,
                        "hired NPC's new work dispatch is feasible at Town Square");
                }
                else
                {
                    Check(rosa.Job == null && rosa.Schedule.All(b => b.ActionType != NpcAction.WorkShift), "closed cart removes owner Job and mechanical shifts");
                    Check(Math.Abs(rosa.Relationships["npc_priya_florist"].Affinity - 0.55f) < 0.0001f,
                        $"closest Family affinity 0.70 -> {rosa.Relationships["npc_priya_florist"].Affinity:0.00}");
                }
            }
            var roster = RosterLoader.LoadFile(path, MvpTownData.CreateRegistry());
            var cold = roster.NpcsById["npc_greta_retired"].Personality;
            var warm = roster.NpcsById["npc_maribel_retired"].Personality;
            Check(JobLossStoryline.FiringChance(4, cold) == 0, "CONTROL: four poor days cannot fire anyone");
            Check(JobLossStoryline.FiringChance(6, cold) > JobLossStoryline.FiringChance(5, cold), "firing chance escalates after each consecutive poor day");
            Check(JobLossStoryline.FiringChance(5, cold) > JobLossStoryline.FiringChance(5, warm),
                $"EMPLOYER control: cold boss {JobLossStoryline.FiringChance(5, cold):0.000} > warm boss {JobLossStoryline.FiringChance(5, warm):0.000}");
            VerifyControls(path, Check);
            log.Add(ok ? "STORYLINE: PASS" : "STORYLINE: FAIL");
            return ok;
        }
        private sealed class ZeroRandom : Random
        {
            public override double NextDouble() => 0;
        }

        private static void VerifyControls(string path, Action<bool, string> check)
        {
            var locations = MvpTownData.CreateRegistry();
            var roster = RosterLoader.LoadFile(path, locations);
            var clock = new GameClock(GameTime.At(256, 0));
            using var story = new JobLossStoryline(clock, roster.Npcs, Employers(), new ZeroRandom());
            var rosa = roster.NpcsById["npc_rosa_shop"];
            void Day() { story.Observe(rosa, 24); clock.AdvanceGameTime(TimeSpan.FromDays(1)); }
            for (int i = 0; i < 4; i++) Day();
            check(rosa.Job != null && story.Progress[rosa.Id].ConsecutivePoorDays == 4,
                "CONTROL: guaranteed firing draw still cannot fire before five poor days");
            rosa.Job.Performance = 0.9f;
            Day();
            check(story.Progress[rosa.Id].ConsecutivePoorDays == 0, "recovered performance resets the consecutive-day counter");
            rosa.Job.Performance = 0.27f;
            for (int i = 0; i < 4; i++) Day();
            check(rosa.Job != null, "CONTROL: earlier poor days are not retained across recovery");
            Day();
            check(rosa.Job == null && story.NeedsDecayMultiplier(rosa) > 1, "fifth new poor day fires and enables unemployment stress");
            var sal = roster.NpcsById["npc_sal_mechanic"];
            rosa.Needs = new Needs { Hunger = 80, Energy = 80, Social = 80, Hygiene = 80 };
            sal.Needs = new Needs { Hunger = 80, Energy = 80, Social = 80, Hygiene = 80 };
            var sim = new NpcSimulator(new UtilityScorer(new FeasibilityEvaluator(locations), new Random(0)), story.NeedsDecayMultiplier);
            sim.StepBackground(rosa, clock.Now, TimeSpan.FromHours(1));
            sim.StepBackground(sal, clock.Now, TimeSpan.FromHours(1));
            check(rosa.Needs.Hunger < sal.Needs.Hunger && Math.Abs(rosa.Needs.Hunger - 71.6f) < 0.001,
                $"real simulator stress control: fired Hunger {rosa.Needs.Hunger:0.0} vs employed {sal.Needs.Hunger:0.0} from identical 80");
            rosa.Money = 0;
            for (int i = 0; i < 4; i++) Day();
            check(!story.Carts.ContainsKey(rosa.Id), "CONTROL: ambition without savings cannot open a cart");
            rosa.Money = 1000;
            rosa.Personality.Ambition = 0;
            Day();
            check(!story.Carts.ContainsKey(rosa.Id), "CONTROL: savings without ambition cannot open a cart");
            rosa.Personality.Ambition = 1;
            Day();
            check(story.Carts.ContainsKey(rosa.Id) && rosa.Money == 800 && story.NeedsDecayMultiplier(rosa) == 1,
                "eligible opening charges startup cost exactly once and ends unemployment stress");
            var schedules = rosa.Schedule;
            bool overlap = false;
            for (int minute = 0; minute < 1440; minute++)
                overlap |= schedules.Count(b => ScheduleService.Contains(b, TimeSpan.FromMinutes(minute))) > 1;
            check(!overlap, "new cart shift preserves a non-overlapping schedule across midnight");

            float PerformanceWithNeeds(float need)
            {
                var testRoster = RosterLoader.LoadFile(path, locations);
                var testNpc = testRoster.NpcsById["npc_rosa_shop"];
                testNpc.Needs = new Needs { Hunger = need, Energy = need, Social = need, Hygiene = need };
                var testClock = new GameClock(GameTime.At(256, 0));
                using var testStory = new JobLossStoryline(testClock, testRoster.Npcs, Employers(), new Random(0));
                testStory.Observe(testNpc, 24);
                testClock.AdvanceGameTime(TimeSpan.FromDays(1));
                return testNpc.Job.Performance;
            }
            float comfortable = PerformanceWithNeeds(80), neglected = PerformanceWithNeeds(10);
            check(neglected < comfortable && comfortable < 0.27f,
                $"performance really drifts: identical Diligence, cared-for {comfortable:0.000} vs neglected {neglected:0.000}");
        }
    }
}
