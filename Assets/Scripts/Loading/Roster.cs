using System.Collections.Generic;
using SideQuest.Data;

namespace SideQuest.Loading
{
    public sealed class Roster
    {
        public IReadOnlyList<NPC> Npcs { get; }
        public IReadOnlyDictionary<string, NPC> NpcsById { get; }

        // Null when the file has no "player" section. npc_roster.json has none; the player is defined elsewhere.
        public PlayerCharacter Player { get; }

        public Roster(List<NPC> npcs, PlayerCharacter player)
        {
            var byId = new Dictionary<string, NPC>();
            foreach (var npc in npcs) byId.Add(npc.Id, npc);

            Npcs = npcs;
            NpcsById = byId;
            Player = player;
        }
    }
}
