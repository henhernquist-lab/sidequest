using System;
using System.Collections.Generic;

namespace SideQuest.Data
{
    public class MemoryFact
    {
        public DateTime Timestamp;
        public string EventType;
        public string Description;
        public float ImportanceScore; // 0.0-1.0, drives retrieval priority
        public List<string> InvolvedNpcIds = new();
    }
}
