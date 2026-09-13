using System.Collections.Generic;

namespace SideQuest.Data
{
    public class PlayerCharacter
    {
        public string DisplayName;
        public int Money;
        public List<string> InventoryItemIds = new();

        public float Karma;        // -1.0 (cruel) to 1.0 (kind) — how you treat people
        public float Notoriety;    // 0.0-1.0 — public chaos/crime level, cop-facing
        public float GigRating;    // 0.0-5.0, Uber-style, gates which gigs appear

        public List<string> CompletedGigIds = new();
        public string CurrentLocationId;
    }
}
