using System;
using SideQuest.Core;
using SideQuest.Data;

namespace SideQuest.Simulation
{
    // LOD-aware stepping (design-doc §5). Only Active NPCs get a utility scoring pass.
    public sealed class NpcSimulator
    {
        private readonly UtilityScorer _scorer;

        public NpcSimulator(UtilityScorer scorer)
        {
            _scorer = scorer;
        }

        // Returns the decision for Active NPCs, or null when the tier ran no scoring pass.
        public NpcDecision Step(NPC npc, GameTime now, TimeSpan duration)
        {
            switch (npc.CurrentTier)
            {
                case SimTier.Active:
                    return StepActive(npc, now, duration);
                case SimTier.Background:
                    StepBackground(npc, now, duration);
                    return null;
                case SimTier.Dormant:
                    return null;
                default:
                    throw new ArgumentOutOfRangeException(nameof(npc.CurrentTier), npc.CurrentTier, "Unhandled SimTier.");
            }
        }

        // The decision is made on the state at `now`; the action then plays out over `duration`.
        public NpcDecision StepActive(NPC npc, GameTime now, TimeSpan duration)
        {
            var scores = _scorer.ScoreAll(npc, now);
            var choice = UtilityScorer.Best(scores);
            NeedsSystem.Decay(npc.Needs, duration.TotalHours);
            NeedsSystem.ApplyAction(npc.Needs, choice.Action, duration.TotalHours);

            npc.CurrentActivity = choice.Action.ToString();
            if (choice.VenueId != null) npc.CurrentLocationId = choice.VenueId;
            npc.LastSimulatedAt = now.Plus(duration).ToDateTime();
            return new NpcDecision(choice, scores);
        }

        public void StepBackground(NPC npc, GameTime now, TimeSpan duration)
        {
            NeedsSystem.Decay(npc.Needs, duration.TotalHours);
            NeedsSystem.ApplyOffscreenFloor(npc.Needs);

            GameTime end = now.Plus(duration);
            ApplyScheduleState(npc, end.TimeOfDay);
            npc.LastSimulatedAt = end.ToDateTime();
        }

        // One batched resolution for however long the NPC was dormant — no per-tick replay.
        public void CatchUp(NPC npc, GameTime now)
        {
            double elapsedHours = GameTime.HoursBetween(GameTime.FromDateTime(npc.LastSimulatedAt), now);
            if (elapsedHours <= 0) return;

            NeedsSystem.Decay(npc.Needs, elapsedHours);
            NeedsSystem.ApplyOffscreenFloor(npc.Needs);
            ApplyScheduleState(npc, now.TimeOfDay);
            npc.LastSimulatedAt = now.ToDateTime();
        }

        private static void ApplyScheduleState(NPC npc, TimeSpan timeOfDay)
        {
            var block = ScheduleService.BlockAt(npc, timeOfDay);
            if (block == null)
            {
                npc.CurrentActivity = NpcAction.Idle.ToString();
                return;
            }

            npc.CurrentActivity = block.ActionType.ToString();
            if (block.LocationId != null) npc.CurrentLocationId = block.LocationId;
        }
    }
}
