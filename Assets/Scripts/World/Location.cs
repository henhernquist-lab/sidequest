using System;

namespace SideQuest.World
{
    public sealed class Location
    {
        public string Id;
        public string DisplayName;
        public OpeningHours Hours;
        public bool IsWorkplace;

        // Capability flags feasibility checks ask about. Not specified by §20 — see STATUS.md.
        public bool ServesFood;
        public bool IsSocialVenue;
        public bool IsResidence;

        public bool IsOpenAt(TimeSpan timeOfDay) => Hours.IsOpenAt(timeOfDay);
    }
}
