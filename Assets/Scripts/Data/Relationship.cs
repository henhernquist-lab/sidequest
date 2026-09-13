using System;

namespace SideQuest.Data
{
    public class Relationship
    {
        public float Affinity;      // -1.0 to 1.0
        public float Trust;         // 0.0-1.0
        public float Familiarity;   // 0.0-1.0, grows with interaction count
        public DateTime LastInteractionAt;
        public RelationshipType Type;
    }
}
