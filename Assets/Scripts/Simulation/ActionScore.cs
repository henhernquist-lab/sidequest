namespace SideQuest.Simulation
{
    // Keeps every term of the score so "why did this NPC do that" has a loggable answer.
    public sealed class ActionScore
    {
        public NpcAction Action { get; }
        public float Urgency { get; }
        public float PersonalityWeight { get; }
        public float Feasibility { get; }
        public float NoiseMultiplier { get; }
        public float Final { get; }
        public bool IsScheduled { get; }
        public string FeasibilityReason { get; }
        public string VenueId { get; }

        public ActionScore(NpcAction action, float urgency, float personalityWeight, FeasibilityResult feasibility,
            float noiseMultiplier, bool isScheduled)
        {
            Action = action;
            Urgency = urgency;
            PersonalityWeight = personalityWeight;
            Feasibility = feasibility.Value;
            NoiseMultiplier = noiseMultiplier;
            IsScheduled = isScheduled;
            FeasibilityReason = feasibility.Reason;
            VenueId = feasibility.VenueId;
            Final = urgency * personalityWeight * feasibility.Value * noiseMultiplier;
        }

        public override string ToString() =>
            $"{Action,-10} final={Final:0.0000} = urg {Urgency:0.000} x wt {PersonalityWeight:0.00} x feas {Feasibility:0} x noise {NoiseMultiplier:0.000}" +
            $"{(IsScheduled ? " [scheduled]" : "")} ({FeasibilityReason})";
    }
}
