using System;
using SideQuest.Data;

namespace SideQuest.Simulation
{
    public static class ScheduleService
    {
        // Start inclusive, End exclusive; End earlier than Start wraps past midnight.
        public static bool Contains(ScheduleBlock block, TimeSpan timeOfDay)
        {
            if (block.End > block.Start) return timeOfDay >= block.Start && timeOfDay < block.End;
            return timeOfDay >= block.Start || timeOfDay < block.End;
        }

        public static ScheduleBlock BlockAt(NPC npc, TimeSpan timeOfDay)
        {
            foreach (var block in npc.Schedule)
            {
                if (Contains(block, timeOfDay)) return block;
            }
            return null;
        }

        public static NpcAction? ScheduledActionAt(NPC npc, TimeSpan timeOfDay)
        {
            var block = BlockAt(npc, timeOfDay);
            return block?.ActionType;
        }
    }
}
