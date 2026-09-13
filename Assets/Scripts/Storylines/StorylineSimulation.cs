using System;
using System.Collections.Generic;
using System.Linq;
using SideQuest.Core;
using SideQuest.Data;
using SideQuest.Simulation;
using SideQuest.World;

namespace SideQuest.Storylines
{
    // Headless host usable by an Editor-side clock driver. Always splits at midnight;
    // observations belong to the day just completed before the clock emits DayStarted.
    public sealed class StorylineSimulation : IDisposable
    {
        private readonly List<NPC> _npcs;
        private readonly NpcSimulator _simulator;
        public GameClock Clock { get; }
        public JobLossStoryline Storyline { get; }

        public StorylineSimulation(GameClock clock, IEnumerable<NPC> npcs, LocationRegistry locations,
            IReadOnlyDictionary<string, string> employers, int seed)
        {
            Clock = clock;
            _npcs = npcs.OrderBy(n => n.Id, StringComparer.Ordinal).ToList();
            Storyline = new JobLossStoryline(clock, _npcs, employers, new Random(seed));
            _simulator = new NpcSimulator(new UtilityScorer(new FeasibilityEvaluator(locations), new Random(seed)),
                Storyline.NeedsDecayMultiplier);
        }

        public void Advance(TimeSpan duration)
        {
            if (duration < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(duration));
            while (duration > TimeSpan.Zero)
            {
                var remainingDay = TimeSpan.FromDays(1) - Clock.Now.TimeOfDay;
                var step = duration < TimeSpan.FromHours(1) ? duration : TimeSpan.FromHours(1);
                if (step > remainingDay) step = remainingDay;
                foreach (var npc in _npcs)
                {
                    _simulator.Step(npc, Clock.Now, step);
                    // Dormant means no observation; no invented work or performance history.
                    if (npc.CurrentTier != SimTier.Dormant) Storyline.Observe(npc, step.TotalHours);
                }
                ShareOneFact();
                Clock.AdvanceGameTime(step);
                duration -= step;
            }
        }

        // A real Socialize decision, same venue, and a trusted/familiar relationship gate
        // propagation. One canonical fact per interaction, no speculative LLM distortion.
        private void ShareOneFact()
        {
            foreach (var speaker in _npcs.Where(n => n.CurrentTier == SimTier.Active && n.CurrentActivity == NpcAction.Socialize.ToString()))
            {
                foreach (var listener in _npcs.Where(n => n.Id != speaker.Id && n.CurrentLocationId == speaker.CurrentLocationId))
                {
                    if (!speaker.Relationships.TryGetValue(listener.Id, out var relationship)
                        || relationship.Affinity < 0.3f || relationship.Familiarity < 0.3f) continue;
                    var fact = speaker.MemoryStream.Where(m => m.EventType == "JobLost" || m.EventType.StartsWith("Business", StringComparison.Ordinal))
                        .OrderByDescending(m => m.Timestamp).FirstOrDefault();
                    if (fact == null || listener.MemoryStream.Any(m => m.EventType == "Shared:" + fact.EventType && m.Description == fact.Description)) continue;
                    listener.MemoryStream.Add(new MemoryFact { Timestamp = Clock.Now.ToDateTime(), EventType = "Shared:" + fact.EventType,
                        Description = fact.Description, ImportanceScore = fact.ImportanceScore * 0.8f,
                        InvolvedNpcIds = new List<string>(fact.InvolvedNpcIds) });
                    break;
                }
            }
        }

        public void Dispose() => Storyline.Dispose();
    }
}
