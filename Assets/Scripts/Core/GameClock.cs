using System;

namespace SideQuest.Core
{
    // Plain C# events rather than a general event bus: §5 calls for a bus, but nothing
    // subscribes to anything except the clock yet, so a bus now would be speculative.
    public sealed class GameClock
    {
        public GameTime Now { get; private set; }

        // Fired once per day boundary crossed, before Ticked, so tick handlers
        // always see daily resets already applied.
        public event Action<int> DayStarted;

        public event Action<GameTime> Ticked;

        public GameClock(GameTime start)
        {
            Now = start;
        }

        public void AdvanceRealSeconds(double realSeconds)
        {
            AdvanceGameTime(TimeSpan.FromMinutes(realSeconds * SimulationTuning.GameMinutesPerRealSecond));
        }

        public void AdvanceGameTime(TimeSpan delta)
        {
            int previousDay = Now.Day;
            Now = Now.Plus(delta);

            for (int day = previousDay + 1; day <= Now.Day; day++)
            {
                DayStarted?.Invoke(day);
            }

            Ticked?.Invoke(Now);
        }
    }
}
