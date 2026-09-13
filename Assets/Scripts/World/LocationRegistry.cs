using System;
using System.Collections.Generic;
using System.Linq;

namespace SideQuest.World
{
    public sealed class LocationRegistry
    {
        private readonly Dictionary<string, Location> _byId;

        public LocationRegistry(IEnumerable<Location> locations)
        {
            _byId = new Dictionary<string, Location>();
            foreach (var location in locations)
            {
                if (_byId.ContainsKey(location.Id))
                    throw new ArgumentException($"Duplicate location id '{location.Id}'.", nameof(locations));
                _byId.Add(location.Id, location);
            }
        }

        public IReadOnlyCollection<Location> All => _byId.Values;

        public bool TryGet(string id, out Location location)
        {
            if (id == null)
            {
                location = null;
                return false;
            }
            return _byId.TryGetValue(id, out location);
        }

        // Unknown ids read as closed rather than throwing: a stale id on an NPC should
        // make an action infeasible, not crash the tick.
        public bool IsOpenAt(string id, TimeSpan timeOfDay) => TryGet(id, out var location) && location.IsOpenAt(timeOfDay);

        public IEnumerable<Location> OpenWhere(Func<Location, bool> predicate, TimeSpan timeOfDay) =>
            _byId.Values.Where(l => predicate(l) && l.IsOpenAt(timeOfDay));
    }
}
