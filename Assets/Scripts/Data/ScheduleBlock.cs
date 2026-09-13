using System;
using SideQuest.Simulation;

namespace SideQuest.Data
{
    public class ScheduleBlock
    {
        public TimeSpan Start;
        public TimeSpan End;
        public string Activity; // Flavor/display only; never used for dispatch.
        public NpcAction ActionType;
        public string LocationId;
    }
}
