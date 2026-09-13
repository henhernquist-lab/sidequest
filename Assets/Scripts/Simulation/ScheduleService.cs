using System;
using System.Collections.Generic;
using SideQuest.Data;

namespace SideQuest.Simulation
{
    public static class ScheduleService
    {
        // ScheduleBlock.Activity is a free-form string in §7. Anything not in this table
        // gets no schedule baseline at all — use FindUnmappedActivities to catch typos,
        // since an unmapped block fails silently otherwise.
        private static readonly Dictionary<string, NpcAction> ActivityToAction =
            new Dictionary<string, NpcAction>(StringComparer.OrdinalIgnoreCase)
            {
                ["Work"] = NpcAction.WorkShift,
                ["Sleep"] = NpcAction.Sleep,
                ["Leisure"] = NpcAction.Socialize,
                ["Eat"] = NpcAction.Eat,
                ["Bathe"] = NpcAction.Bathe,
                ["Goal"] = NpcAction.PursueGoal,
                ["Idle"] = NpcAction.Idle,
            };

        public static bool TryMapActivity(string activity, out NpcAction action)
        {
            action = default;
            return activity != null && ActivityToAction.TryGetValue(activity, out action);
        }

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
            return block != null && TryMapActivity(block.Activity, out var action) ? action : (NpcAction?)null;
        }

        public static List<string> FindUnmappedActivities(NPC npc)
        {
            var unmapped = new List<string>();
            foreach (var block in npc.Schedule)
            {
                if (!TryMapActivity(block.Activity, out _)) unmapped.Add(block.Activity ?? "<null>");
            }
            return unmapped;
        }
    }
}
