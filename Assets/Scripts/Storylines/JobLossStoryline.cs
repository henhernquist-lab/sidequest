using System;
using System.Collections.Generic;
using System.Linq;
using SideQuest.Core;
using SideQuest.Data;
using SideQuest.Simulation;
using SideQuest.World;

namespace SideQuest.Storylines
{
    // Host observes simulated intervals, then advances the clock. Daily decisions use those
    // observations; no LLM, NPC-name triggers, or player proximity gates. Dispose detaches reset.
    public sealed class JobLossStoryline : IDisposable
    {
        private readonly GameClock _clock;
        private readonly Dictionary<string, NPC> _npcs;
        private readonly Dictionary<string, string> _employers;
        private readonly Dictionary<string, EmploymentProgress> _progress;
        private readonly Dictionary<string, FoodCart> _carts = new Dictionary<string, FoodCart>();
        private readonly HashSet<string> _visitors = new HashSet<string>();
        private readonly Random _random;
        private int _lastDay;
        public IReadOnlyDictionary<string, EmploymentProgress> Progress => _progress;
        public IReadOnlyDictionary<string, FoodCart> Carts => _carts;
        public int LastFootTraffic { get; private set; }

        public JobLossStoryline(GameClock clock, IEnumerable<NPC> npcs,
            IReadOnlyDictionary<string, string> employerNpcByWorkplace, Random random)
        {
            _clock = clock ?? throw new ArgumentNullException(nameof(clock));
            _random = random ?? throw new ArgumentNullException(nameof(random));
            _npcs = npcs.ToDictionary(n => n.Id, StringComparer.Ordinal);
            _employers = employerNpcByWorkplace.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            foreach (var pair in _employers)
                if (!_npcs.ContainsKey(pair.Value)) throw new ArgumentException("Unknown employer NPC: " + pair.Value);
            foreach (var npc in _npcs.Values.Where(n => n.Job != null))
                if (!_employers.ContainsKey(npc.Job.WorkplaceId))
                    throw new ArgumentException("Missing employer for workplace: " + npc.Job.WorkplaceId);
            _progress = _npcs.Keys.ToDictionary(id => id, id => new EmploymentProgress());
            _lastDay = clock.Now.Day;
            clock.DayStarted += OnDayStarted;
        }

        public void Dispose() => _clock.DayStarted -= OnDayStarted;

        public float NeedsDecayMultiplier(NPC npc) => npc.Job == null && _progress[npc.Id].LostJobDay.HasValue
            ? StorylineTuning.UnemployedDecayMultiplier : 1f;

        // Call after each actual simulation step, before clock rollover. Snapshot before-state
        // needs separately if desired; neglect here is measured from the resulting world state.
        public void Observe(NPC npc, double hours)
        {
            if (double.IsNaN(hours) || double.IsInfinity(hours) || hours <= 0 || hours > 24)
                throw new ArgumentOutOfRangeException(nameof(hours));
            var state = _progress[npc.Id];
            if (state.ObservedJob != npc.Job)
            {
                state.ObservedJob = npc.Job;
                state.ObservedHours = state.NeglectedHours = state.WorkedHours = 0;
                state.ConsecutivePoorDays = 0;
            }
            if (state.ObservedHours + hours > 24.000001) throw new InvalidOperationException("More than 24 observed hours for one NPC/day.");
            state.ObservedHours += hours;
            var needs = npc.Needs;
            int neglected = (needs.Hunger < StorylineTuning.NeglectedNeed ? 1 : 0)
                + (needs.Energy < StorylineTuning.NeglectedNeed ? 1 : 0)
                + (needs.Social < StorylineTuning.NeglectedNeed ? 1 : 0)
                + (needs.Hygiene < StorylineTuning.NeglectedNeed ? 1 : 0);
            state.NeglectedHours += hours * neglected / 4.0;
            if (npc.Job != null && npc.CurrentActivity == NpcAction.WorkShift.ToString()
                && npc.CurrentLocationId == npc.Job.WorkplaceId) state.WorkedHours += hours;
            if (_clock.Now.TimeOfDay >= TimeSpan.FromHours(10) && _clock.Now.TimeOfDay < TimeSpan.FromHours(16)
                && npc.CurrentLocationId == MvpTownData.TownSquare && !_carts.ContainsKey(npc.Id)
                && !_carts.Values.Any(c => c.EmployeeId == npc.Id)) _visitors.Add(npc.Id);
        }

        public static float FiringChance(int consecutivePoorDays, Personality employer)
        {
            if (consecutivePoorDays < StorylineTuning.PoorDaysBeforeReview) return 0;
            float severity = 1f + (1f - employer.Warmth) * 0.75f + (1f - employer.Honesty) * 0.25f;
            return Math.Clamp((consecutivePoorDays - StorylineTuning.PoorDaysBeforeReview + 1)
                * StorylineTuning.DailyFiringEscalation * severity, 0f, 1f);
        }

        private void OnDayStarted(int day)
        {
            if (day <= _lastDay) return;
            // Skipped days without observations don't fabricate performance history or wages.
            int elapsed = day - _lastDay;
            _lastDay = day;
            LastFootTraffic = _visitors.Count;
            var observedToday = new HashSet<string>(_progress.Where(p => p.Value.ObservedHours > 0).Select(p => p.Key));
            foreach (var npc in _npcs.Values.OrderBy(n => n.Id, StringComparer.Ordinal))
            {
                var state = _progress[npc.Id];
                state.IncomePaidToday = 0;
                state.LastFiringChance = 0;
                if (state.ObservedHours <= 0) { state.ConsecutivePoorDays = 0; continue; }
                if (elapsed != 1) state.ConsecutivePoorDays = 0;
                if (npc.Job != null && state.ObservedJob == npc.Job)
                {
                    // Income is hourly, paid for observed work; accumulated fractions are retained.
                    state.WageRemainder += npc.Job.Income * state.WorkedHours;
                    state.IncomePaidToday = (int)Math.Floor(state.WageRemainder);
                    npc.Money = checked(npc.Money + state.IncomePaidToday);
                    state.WageRemainder -= state.IncomePaidToday;
                    npc.Job.Performance = Math.Clamp(npc.Job.Performance
                        + (StorylineTuning.PerformanceDrift * (npc.Personality.Diligence - 0.5f)
                        - StorylineTuning.NeglectPenalty * (float)(state.NeglectedHours / state.ObservedHours))
                        * (float)(state.ObservedHours / 24.0), 0f, 1f);
                    // Cart owners cannot fire themselves; hired workers belong to their cart owner.
                    if (!_carts.ContainsKey(npc.Id))
                    {
                        state.ConsecutivePoorDays = state.ObservedHours >= 24 && npc.Job.Performance < StorylineTuning.PoorPerformance
                            ? state.ConsecutivePoorDays + 1 : 0;
                        string employerId = EmployerFor(npc);
                        state.LastFiringChance = FiringChance(state.ConsecutivePoorDays, _npcs[employerId].Personality);
                        if (state.LastFiringChance > 0 && _random.NextDouble() < state.LastFiringChance)
                            Fire(npc, employerId, day);
                    }
                }
                else if (npc.Job == null && state.LostJobDay.HasValue && !_carts.ContainsKey(npc.Id)
                    && day - state.LostJobDay.Value >= StorylineTuning.BusinessWaitDays
                    && npc.Personality.Ambition >= StorylineTuning.MinimumAmbition
                    && npc.Money >= StorylineTuning.CartStartupCost) OpenCart(npc, day);
                state.ObservedHours = state.NeglectedHours = state.WorkedHours = 0;
            }
            foreach (var cart in _carts.Values.OrderBy(c => c.OwnerId, StringComparer.Ordinal).ToList())
                if (observedToday.Contains(cart.OwnerId) && cart.Outcome == FoodCartOutcome.Operating && cart.OpenedDay < day) UpdateCart(cart, day);
            _visitors.Clear();
        }

        private string EmployerFor(NPC npc)
        {
            var cart = _carts.Values.FirstOrDefault(c => c.EmployeeId == npc.Id && c.Outcome != FoodCartOutcome.Closed);
            if (cart != null) return cart.OwnerId;
            if (_employers.TryGetValue(npc.Job.WorkplaceId, out string id)) return id;
            throw new InvalidOperationException("Missing employer for workplace: " + npc.Job.WorkplaceId);
        }

        private void Fire(NPC npc, string employerId, int day)
        {
            string location = npc.Job.WorkplaceId;
            npc.Job = null;
            var state = _progress[npc.Id];
            state.LostJobDay = day;
            state.ConsecutivePoorDays = 0;
            ReplaceWork(npc, NpcAction.PursueGoal, npc.HomeLocationId, "Looking for a new start");
            WriteFact("JobLost", $"{npc.DisplayName} was fired by {_npcs[employerId].DisplayName}.",
                day, location, npc.Id, employerId);
        }

        private void OpenCart(NPC npc, int day)
        {
            npc.Money -= StorylineTuning.CartStartupCost;
            var cart = new FoodCart { OwnerId = npc.Id, LocationId = MvpTownData.TownSquare,
                OpenedDay = day, Health = StorylineTuning.StartingHealth, Outcome = FoodCartOutcome.Operating };
            _carts.Add(npc.Id, cart);
            AssignCartJob(npc, "Food cart owner", StorylineTuning.CartHourlyIncome);
            WriteFact("BusinessOpened", $"{npc.DisplayName} opened a food cart at Town Square.", day, cart.LocationId, npc.Id);
        }

        private void UpdateCart(FoodCart cart, int day)
        {
            var owner = _npcs[cart.OwnerId];
            float traffic = Math.Clamp(LastFootTraffic / StorylineTuning.FullTrafficVisitors, 0f, 1f);
            cart.Health = Math.Clamp(cart.Health + StorylineTuning.TrafficGrowth * (traffic - 0.5f)
                + StorylineTuning.DiligenceGrowth * (owner.Personality.Diligence - 0.5f)
                + StorylineTuning.DailyHealthNoise * (float)(_random.NextDouble() * 2 - 1), 0f, 1f);
            if (cart.Health <= StorylineTuning.FailureHealth)
            {
                cart.Outcome = FoodCartOutcome.Closed;
                owner.Job = null;
                _progress[owner.Id].LostJobDay = day;
                ReplaceWork(owner, NpcAction.PursueGoal, owner.HomeLocationId, "Rebuilding after the cart closed");
                var close = owner.Relationships.Where(r => r.Value.Type == RelationshipType.Family || r.Value.Type == RelationshipType.Romantic)
                    .OrderByDescending(r => r.Value.Affinity).ThenBy(r => r.Key, StringComparer.Ordinal).FirstOrDefault();
                var involved = new List<string> { owner.Id };
                if (close.Value != null)
                {
                    close.Value.Affinity = Math.Clamp(close.Value.Affinity - StorylineTuning.RelationshipStress, -1f, 1f);
                    if (_npcs[close.Key].Relationships.TryGetValue(owner.Id, out var reverse))
                        reverse.Affinity = Math.Clamp(reverse.Affinity - StorylineTuning.RelationshipStress, -1f, 1f);
                    involved.Add(close.Key);
                }
                WriteFact("BusinessFailed", $"{owner.DisplayName}'s food cart closed; the stress strained their closest family or partner tie.",
                    day, cart.LocationId, involved.ToArray());
            }
            else if (cart.Health >= StorylineTuning.SuccessHealth)
            {
                var candidate = _npcs.Values.Where(n => n.Id != owner.Id && n.Job == null && n.Age >= 18 && !_carts.ContainsKey(n.Id))
                    .OrderByDescending(n => n.Personality.Diligence).ThenBy(n => n.Id, StringComparer.Ordinal).FirstOrDefault();
                if (candidate == null) return; // Successful enough, but wait until a real worker is available.
                AssignCartJob(candidate, "Food cart assistant", StorylineTuning.CartEmployeeHourlyIncome);
                cart.EmployeeId = candidate.Id;
                cart.Outcome = FoodCartOutcome.Hired;
                WriteFact("BusinessHired", $"{owner.DisplayName}'s food cart prospered and hired {candidate.DisplayName}.",
                    day, cart.LocationId, owner.Id, candidate.Id);
            }
        }

        private static void ReplaceWork(NPC npc, NpcAction action, string location, string label)
        {
            foreach (var block in npc.Schedule.Where(b => b.ActionType == NpcAction.WorkShift))
            { block.ActionType = action; block.LocationId = location; block.Activity = label; }
            npc.CurrentActivity = NpcAction.Idle.ToString();
        }

        private static void AssignCartJob(NPC npc, string title, int income)
        {
            npc.Job = new Job { Title = title, WorkplaceId = MvpTownData.TownSquare, Income = income, Performance = 0.6f };
            // Carve 10–16 out of existing daily intervals, preserving outside time (including wrapped sleep).
            var blocks = new List<ScheduleBlock>();
            foreach (var block in npc.Schedule)
            {
                double start = block.Start.TotalHours, end = block.End.TotalHours;
                var segments = end > start ? new[] { (start, end) } : new[] { (start, 24d), (0d, end) };
                foreach (var segment in segments)
                {
                    void Keep(double a, double b)
                    {
                        if (b <= a) return;
                        blocks.Add(new ScheduleBlock { Start = TimeSpan.FromHours(a), End = TimeSpan.FromHours(b == 24 ? 0 : b),
                            Activity = block.Activity, ActionType = block.ActionType, LocationId = block.LocationId });
                    }
                    Keep(segment.Item1, Math.Min(segment.Item2, 10));
                    Keep(Math.Max(segment.Item1, 16), segment.Item2);
                }
            }
            blocks.Add(new ScheduleBlock { Start = TimeSpan.FromHours(10), End = TimeSpan.FromHours(16),
                Activity = title, ActionType = NpcAction.WorkShift, LocationId = MvpTownData.TownSquare });
            npc.Schedule = blocks.OrderBy(b => b.Start).ToList();
        }

        private void WriteFact(string type, string description, int day, string location, params string[] involved)
        {
            var ids = involved.Distinct().OrderBy(id => id, StringComparer.Ordinal).ToList();
            var recipients = new HashSet<string>(ids);
            foreach (string id in ids)
                foreach (var relation in _npcs[id].Relationships)
                    if (relation.Value.Type == RelationshipType.Family || relation.Value.Type == RelationshipType.Romantic
                        || relation.Value.Affinity >= StorylineTuning.CloseAffinity) recipients.Add(relation.Key);
            foreach (var npc in _npcs.Values)
                if (npc.CurrentLocationId == location) recipients.Add(npc.Id);
            foreach (string id in recipients)
                _npcs[id].MemoryStream.Add(new MemoryFact { Timestamp = GameTime.At(day, 0).ToDateTime(), EventType = type,
                    Description = description, ImportanceScore = 0.8f, InvolvedNpcIds = new List<string>(ids) });
        }
    }
}
