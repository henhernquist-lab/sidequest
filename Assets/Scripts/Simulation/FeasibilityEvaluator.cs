using System;
using System.Linq;
using SideQuest.Core;
using SideQuest.Data;
using SideQuest.World;

namespace SideQuest.Simulation
{
    public readonly struct FeasibilityResult
    {
        public float Value { get; }
        public string Reason { get; }
        public string VenueId { get; }

        private FeasibilityResult(float value, string reason, string venueId)
        {
            Value = value;
            Reason = reason;
            VenueId = venueId;
        }

        public static FeasibilityResult Possible(string reason, string venueId) => new FeasibilityResult(1f, reason, venueId);
        public static FeasibilityResult Impossible(string reason) => new FeasibilityResult(0f, reason, null);
    }

    // Binary for now: 1 = possible, 0 = impossible. Distance isn't checked because the
    // §7 data model has no spatial position, only CurrentLocationId.
    public sealed class FeasibilityEvaluator
    {
        private readonly LocationRegistry _locations;

        public FeasibilityEvaluator(LocationRegistry locations)
        {
            _locations = locations;
        }

        public FeasibilityResult Evaluate(NPC npc, NpcAction action, GameTime now)
        {
            TimeSpan tod = now.TimeOfDay;
            string scheduledVenueId = ScheduledVenueFor(npc, action, tod);

            switch (action)
            {
                case NpcAction.Eat:
                    return TryFindVenue(npc, l => l.ServesFood, tod, scheduledVenueId, out var foodVenue)
                        ? FeasibilityResult.Possible($"food at {foodVenue.Id}", foodVenue.Id)
                        : FeasibilityResult.Impossible("no open food source and no usable home");

                case NpcAction.Socialize:
                    return TryFindVenue(npc, l => l.IsSocialVenue, tod, scheduledVenueId, out var socialVenue)
                        ? FeasibilityResult.Possible($"social venue {socialVenue.Id}", socialVenue.Id)
                        : FeasibilityResult.Impossible("no open social venue");

                case NpcAction.Sleep:
                case NpcAction.Bathe:
                    return TryGetUsableHome(npc, tod, out var home)
                        ? FeasibilityResult.Possible("at home", home.Id)
                        : FeasibilityResult.Impossible($"home '{npc.HomeLocationId}' missing or not a residence");

                case NpcAction.WorkShift:
                    return EvaluateWorkShift(npc, tod);

                case NpcAction.PursueGoal:
                    return npc.Goals != null && npc.Goals.ShortTerm.Count + npc.Goals.LongTerm.Count > 0
                        ? FeasibilityResult.Possible("has goals", npc.CurrentLocationId)
                        : FeasibilityResult.Impossible("no goals");

                case NpcAction.Idle:
                    return FeasibilityResult.Possible("always available", npc.CurrentLocationId);

                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, "Unhandled NpcAction.");
            }
        }

        private FeasibilityResult EvaluateWorkShift(NPC npc, TimeSpan tod)
        {
            if (npc.Job == null)
                return FeasibilityResult.Impossible("unemployed");
            if (ScheduleService.ScheduledActionAt(npc, tod) != NpcAction.WorkShift)
                return FeasibilityResult.Impossible("not shift time");
            if (!_locations.TryGet(npc.Job.WorkplaceId, out var workplace))
                return FeasibilityResult.Impossible($"workplace '{npc.Job.WorkplaceId}' not in registry");
            if (!workplace.IsOpenAt(tod))
                return FeasibilityResult.Impossible($"{workplace.Id} closed ({workplace.Hours})");
            return FeasibilityResult.Possible($"shift at {workplace.Id}", workplace.Id);
        }

        // Only set when this action is the one the schedule calls for right now.
        private static string ScheduledVenueFor(NPC npc, NpcAction action, TimeSpan tod)
        {
            var block = ScheduleService.BlockAt(npc, tod);
            return block != null && ScheduleService.TryMapActivity(block.Activity, out var scheduled) && scheduled == action
                ? block.LocationId
                : null;
        }

        // Prefers the scheduled location, then where the NPC already is, then their home,
        // then the first open public venue by id (arbitrary but deterministic — no distance data).
        private bool TryFindVenue(NPC npc, Func<Location, bool> capability, TimeSpan tod, string scheduledVenueId, out Location venue)
        {
            foreach (var candidateId in new[] { scheduledVenueId, npc.CurrentLocationId, npc.HomeLocationId })
            {
                if (_locations.TryGet(candidateId, out var candidate) && capability(candidate) && IsUsableBy(npc, candidate, tod))
                {
                    venue = candidate;
                    return true;
                }
            }

            venue = _locations.All
                .Where(l => capability(l) && IsUsableBy(npc, l, tod))
                .OrderBy(l => l.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            return venue != null;
        }

        private bool TryGetUsableHome(NPC npc, TimeSpan tod, out Location home) =>
            _locations.TryGet(npc.HomeLocationId, out home) && home.IsResidence && home.IsOpenAt(tod);

        // A residence only counts for the NPC who lives there.
        private static bool IsUsableBy(NPC npc, Location location, TimeSpan tod) =>
            location.IsOpenAt(tod) && (!location.IsResidence || location.Id == npc.HomeLocationId);
    }
}
