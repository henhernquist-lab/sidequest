using System;

namespace SideQuest.Storylines
{
    public enum FoodCartOutcome { Operating, Hired, Closed }

    // One template, located at Town Square. This is a workplace, not a seventh town location.
    public sealed class FoodCart
    {
        public string OwnerId { get; internal set; }
        public string LocationId { get; internal set; }
        public int OpenedDay { get; internal set; }
        public float Health { get; internal set; }
        public FoodCartOutcome Outcome { get; internal set; }
        public string EmployeeId { get; internal set; }
    }

    public sealed class EmploymentProgress
    {
        public int ConsecutivePoorDays { get; internal set; }
        public int? LostJobDay { get; internal set; }
        public int IncomePaidToday { get; internal set; }
        public float LastFiringChance { get; internal set; }
        internal double ObservedHours;
        internal double NeglectedHours;
        internal double WorkedHours;
        internal double WageRemainder;
        internal SideQuest.Data.Job ObservedJob;
    }
}
