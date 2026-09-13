using System;
using System.Collections.Generic;

namespace SideQuest.Data
{
    public class NPC
    {
        public string Id;
        public string DisplayName;
        public int Age;
        public string AppearanceRef; // points to a Blockbench model/skin

        public Personality Personality;
        public Needs Needs;
        public Job Job;              // nullable — unemployed NPCs exist
        public string HomeLocationId;
        public List<string> InventoryItemIds = new();
        public int Money;

        public List<ScheduleBlock> Schedule = new();
        public Goals Goals;
        public Dictionary<string, Relationship> Relationships = new(); // keyed by other NPC's Id
        public List<MemoryFact> MemoryStream = new();

        public SimTier CurrentTier;      // Active | Background | Dormant
        public DateTime LastSimulatedAt;
        public string CurrentLocationId;
        public string CurrentActivity;
    }
}
