using System;
using System.Collections.Generic;
using SideQuest.Core;
using SideQuest.Data;

namespace SideQuest.Simulation
{
    // score = urgency x personality weight x feasibility x noise.
    // Following the schedule is not a separate system: the scheduled action just gets
    // ScheduleBaselineUrgency as a floor, and needs override it only by out-scoring it.
    public sealed class UtilityScorer
    {
        private static readonly NpcAction[] AllActions = (NpcAction[])Enum.GetValues(typeof(NpcAction));

        private readonly FeasibilityEvaluator _feasibility;
        private readonly Random _random;

        public UtilityScorer(FeasibilityEvaluator feasibility, Random random)
        {
            _feasibility = feasibility;
            _random = random;
        }

        public List<ActionScore> ScoreAll(NPC npc, GameTime now)
        {
            NpcAction? scheduled = ScheduleService.ScheduledActionAt(npc, now.TimeOfDay);
            var scores = new List<ActionScore>(AllActions.Length);

            foreach (var action in AllActions)
            {
                bool isScheduled = scheduled == action;
                float urgency = Urgency(npc.Needs, action, isScheduled);
                float weight = PersonalityWeight(npc.Personality, action);
                var feasibility = _feasibility.Evaluate(npc, action, now);
                float noise = 1f + ((float)_random.NextDouble() * 2f - 1f) * SimulationTuning.ScoreNoiseFraction;
                scores.Add(new ActionScore(action, urgency, weight, feasibility, noise, isScheduled));
            }

            return scores;
        }

        public ActionScore Choose(NPC npc, GameTime now) => Best(ScoreAll(npc, now));

        public static ActionScore Best(List<ActionScore> scores)
        {
            ActionScore best = scores[0];
            for (int i = 1; i < scores.Count; i++)
            {
                if (scores[i].Final > best.Final) best = scores[i];
            }
            return best;
        }

        public static float Urgency(Needs needs, NpcAction action, bool isScheduled)
        {
            float needUrgency = action switch
            {
                NpcAction.Eat => ResponseCurve.Urgency(needs.Hunger),
                NpcAction.Sleep => ResponseCurve.Urgency(needs.Energy),
                NpcAction.Socialize => ResponseCurve.Urgency(needs.Social),
                NpcAction.Bathe => ResponseCurve.Urgency(needs.Hygiene),
                NpcAction.PursueGoal => SimulationTuning.GoalDriveUrgency,
                NpcAction.Idle => SimulationTuning.IdleUrgency,
                _ => 0f,
            };
            float scheduleUrgency = isScheduled ? SimulationTuning.ScheduleBaselineUrgency : 0f;
            return Math.Max(needUrgency, scheduleUrgency);
        }

        public static float PersonalityWeight(Personality personality, NpcAction action) => action switch
        {
            NpcAction.Socialize => TraitWeight(personality.Extroversion),
            NpcAction.WorkShift => TraitWeight(personality.Diligence),
            NpcAction.PursueGoal => TraitWeight(personality.Ambition),
            _ => SimulationTuning.NeutralPersonalityWeight,
        };

        private static float TraitWeight(float trait) =>
            SimulationTuning.PersonalityWeightBase + SimulationTuning.PersonalityWeightSpan * Math.Clamp(trait, 0f, 1f);
    }
}
