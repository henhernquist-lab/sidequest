using System;
using SideQuest.Core;

namespace SideQuest.Simulation
{
    public static class ResponseCurve
    {
        // Need values are 0-100 where 100 is fully satisfied. Urgency is deliberately
        // nonlinear: it stays near zero while a need is comfortable and climbs steeply
        // as the need empties. Flattening this to a linear read makes NPCs feel twitchy.
        public static float Urgency(float needValue)
        {
            float deficit = Math.Clamp((100f - needValue) / 100f, 0f, 1f);
            return MathF.Pow(deficit, SimulationTuning.UrgencyCurveExponent);
        }
    }
}
