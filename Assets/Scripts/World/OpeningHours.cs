using System;

namespace SideQuest.World
{
    public readonly struct OpeningHours
    {
        public bool AlwaysOpen { get; }
        public TimeSpan Open { get; }
        public TimeSpan Close { get; }

        private OpeningHours(bool alwaysOpen, TimeSpan open, TimeSpan close)
        {
            AlwaysOpen = alwaysOpen;
            Open = open;
            Close = close;
        }

        public static OpeningHours Always => new OpeningHours(true, TimeSpan.Zero, TimeSpan.Zero);

        public static OpeningHours Between(int openHour, int closeHour) =>
            new OpeningHours(false, TimeSpan.FromHours(openHour), TimeSpan.FromHours(closeHour));

        // Open is inclusive, Close is exclusive. Close earlier than Open means the
        // hours wrap past midnight (e.g. 18:00-02:00).
        public bool IsOpenAt(TimeSpan timeOfDay)
        {
            if (AlwaysOpen) return true;
            if (Close > Open) return timeOfDay >= Open && timeOfDay < Close;
            return timeOfDay >= Open || timeOfDay < Close;
        }

        public override string ToString() => AlwaysOpen ? "always open" : $"{Open:hh\\:mm}-{Close:hh\\:mm}";
    }
}
