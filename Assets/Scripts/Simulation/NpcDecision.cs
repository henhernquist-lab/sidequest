using System.Collections.Generic;

namespace SideQuest.Simulation
{
    public sealed class NpcDecision
    {
        public ActionScore Chosen { get; }
        public IReadOnlyList<ActionScore> AllScores { get; }

        public NpcDecision(ActionScore chosen, IReadOnlyList<ActionScore> allScores)
        {
            Chosen = chosen;
            AllScores = allScores;
        }
    }
}
