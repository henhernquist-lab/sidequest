namespace SideQuest.Core
{
    // Every tunable number for the clock, needs, and utility AI lives here and nowhere else.
    public static class SimulationTuning
    {
        // ---- Clock ----
        // 12 game minutes per real second => one in-game day takes 2 real minutes.
        public const double GameMinutesPerRealSecond = 12.0;

        // ---- Needs decay (points lost per in-game hour, on the 0-100 scale) ----
        // Chosen so a barista's 16h waking day ends with Energy near 30 at bedtime, hunger
        // forces ~3 meals, and hygiene/social don't pull her off an 8h shift.
        public const float HungerDecayPerHour = 7.0f;
        public const float EnergyDecayPerHour = 3.5f;
        public const float SocialDecayPerHour = 2.5f;
        public const float HygieneDecayPerHour = 3.0f;

        // ---- Needs restored per in-game hour while performing the matching action ----
        public const float EatRestorePerHour = 55.0f;
        public const float SleepRestorePerHour = 12.0f;
        public const float SocializeRestorePerHour = 30.0f;
        public const float BatheRestorePerHour = 60.0f;

        // Background/Dormant NPCs aren't simulated action-by-action, so nothing restores
        // their needs. Clamping at this floor stands in for "they fed and rested themselves
        // off-screen." Applied only on the Background/catch-up paths, never on Active.
        public const float OffscreenSubsistenceFloor = 30.0f;

        // ---- Utility scoring ----
        // urgency = deficit ^ exponent, where deficit = (100 - need) / 100.
        // With 3.0: need 70 -> 0.027 (barely registers), need 15 -> 0.614 (spikes hard).
        public const float UrgencyCurveExponent = 3.0f;

        // Urgency floor granted to whichever action the schedule says to do right now.
        // A need-driven action overrides the schedule only by out-scoring this.
        public const float ScheduleBaselineUrgency = 0.35f;

        // Paid shifts are stronger commitments than optional daily activities.
        // Diligence still scales this, and urgent needs can still outweigh work.
        public const float WorkShiftBaselineUrgency = 0.6f;

        // Even low-diligence employees have a paid commitment; diligence strengthens it.
        // Work scores range 0.45..0.60 before noise, below Hunger-10 urgency (~0.729).
        public const float WorkDiligenceWeightBase = 0.75f;
        public const float WorkDiligenceWeightSpan = 0.25f;

        // Goals aren't modeled mechanically yet, so PursueGoal gets a flat drive that
        // Ambition then scales. Kept above IdleUrgency so a goal beats doing nothing.
        public const float GoalDriveUrgency = 0.12f;

        public const float IdleUrgency = 0.05f;

        // Trait-driven weight = Base + Span * trait, so trait 0..1 maps to 0.5..1.5.
        // Personality modulates a score; it can't erase one.
        public const float PersonalityWeightBase = 0.5f;
        public const float PersonalityWeightSpan = 1.0f;

        // Weight for actions no personality trait drives (Eat, Sleep, Bathe, Idle).
        public const float NeutralPersonalityWeight = 1.0f;

        // Multiplicative jitter: final *= 1 +/- up to this fraction. Must stay multiplicative —
        // additive noise would give a feasibility-zero action a nonzero score.
        public const float ScoreNoiseFraction = 0.08f;
    }
}
