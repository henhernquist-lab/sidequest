using System;

namespace SideQuest.Core
{
    // In-game time as (1-based day number, time of day). The §7 data model stores
    // timestamps as DateTime, so ToDateTime/FromDateTime bridge the two via GameEpoch.
    public readonly struct GameTime : IEquatable<GameTime>, IComparable<GameTime>
    {
        public static readonly DateTime GameEpoch = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);

        public int Day { get; }
        public TimeSpan TimeOfDay { get; }

        public GameTime(int day, TimeSpan timeOfDay)
        {
            if (day < 1) throw new ArgumentOutOfRangeException(nameof(day), day, "Day is 1-based.");
            if (timeOfDay < TimeSpan.Zero || timeOfDay >= OneDay)
                throw new ArgumentOutOfRangeException(nameof(timeOfDay), timeOfDay, "TimeOfDay must be in [00:00, 24:00).");
            Day = day;
            TimeOfDay = timeOfDay;
        }

        public static GameTime At(int day, int hour, int minute = 0) => new GameTime(day, new TimeSpan(hour, minute, 0));

        public double TotalHours => (Day - 1) * 24.0 + TimeOfDay.TotalHours;

        public GameTime Plus(TimeSpan delta)
        {
            if (delta < TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(delta), delta, "Game time only moves forward.");
            long totalTicks = TimeOfDay.Ticks + delta.Ticks;
            int daysAdded = (int)(totalTicks / OneDay.Ticks);
            return new GameTime(Day + daysAdded, TimeSpan.FromTicks(totalTicks % OneDay.Ticks));
        }

        public static double HoursBetween(GameTime from, GameTime to) => to.TotalHours - from.TotalHours;

        public DateTime ToDateTime() => GameEpoch.AddDays(Day - 1).Add(TimeOfDay);

        public static GameTime FromDateTime(DateTime value)
        {
            TimeSpan sinceEpoch = value - GameEpoch;
            if (sinceEpoch < TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(value), value, "DateTime is before GameEpoch.");
            return new GameTime((int)(sinceEpoch.Ticks / OneDay.Ticks) + 1, TimeSpan.FromTicks(sinceEpoch.Ticks % OneDay.Ticks));
        }

        public bool Equals(GameTime other) => Day == other.Day && TimeOfDay == other.TimeOfDay;
        public override bool Equals(object obj) => obj is GameTime other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Day, TimeOfDay);
        public int CompareTo(GameTime other) => TotalHours.CompareTo(other.TotalHours);

        public static bool operator ==(GameTime a, GameTime b) => a.Equals(b);
        public static bool operator !=(GameTime a, GameTime b) => !a.Equals(b);

        public override string ToString() => $"Day {Day} {TimeOfDay:hh\\:mm}";
    }
}
