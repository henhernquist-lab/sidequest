using System;
using SideQuest.Core;
using SideQuest.Data;

namespace SideQuest.Simulation
{
    public static class NeedsSystem
    {
        public static void Decay(Needs needs, double gameHours)
        {
            float h = (float)gameHours;
            needs.Hunger = Clamp(needs.Hunger - SimulationTuning.HungerDecayPerHour * h);
            needs.Energy = Clamp(needs.Energy - SimulationTuning.EnergyDecayPerHour * h);
            needs.Social = Clamp(needs.Social - SimulationTuning.SocialDecayPerHour * h);
            needs.Hygiene = Clamp(needs.Hygiene - SimulationTuning.HygieneDecayPerHour * h);
        }

        public static void ApplyAction(Needs needs, NpcAction action, double gameHours)
        {
            float h = (float)gameHours;
            switch (action)
            {
                case NpcAction.Eat:
                    needs.Hunger = Clamp(needs.Hunger + SimulationTuning.EatRestorePerHour * h);
                    break;
                case NpcAction.Sleep:
                    needs.Energy = Clamp(needs.Energy + SimulationTuning.SleepRestorePerHour * h);
                    break;
                case NpcAction.Socialize:
                    needs.Social = Clamp(needs.Social + SimulationTuning.SocializeRestorePerHour * h);
                    break;
                case NpcAction.Bathe:
                    needs.Hygiene = Clamp(needs.Hygiene + SimulationTuning.BatheRestorePerHour * h);
                    break;
            }
        }

        // Note this can raise a need, not just stop its decline: an NPC who leaves the
        // Active tier starving comes back at the floor.
        public static void ApplyOffscreenFloor(Needs needs)
        {
            float floor = SimulationTuning.OffscreenSubsistenceFloor;
            needs.Hunger = Math.Max(needs.Hunger, floor);
            needs.Energy = Math.Max(needs.Energy, floor);
            needs.Social = Math.Max(needs.Social, floor);
            needs.Hygiene = Math.Max(needs.Hygiene, floor);
        }

        private static float Clamp(float value) => Math.Clamp(value, 0f, 100f);
    }
}
