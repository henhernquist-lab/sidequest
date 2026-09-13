using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SideQuest.Core;
using SideQuest.Data;
using SideQuest.World;

namespace SideQuest.Loading
{
    // Loads roster JSON into §7 objects. Every problem in the file is collected and reported together, and if
    // there is even one, Load throws instead of returning, so a half-built or defaulted NPC never reaches the
    // simulation (design-doc §15 Implementation Risk #5).
    public static class RosterLoader
    {
        private static readonly string[] NpcFields =
        {
            "id", "displayName", "age", "appearanceRef", "personality", "needs", "job", "homeLocationId",
            "inventoryItemIds", "money", "schedule", "goals", "relationships", "memoryStream",
            "currentTier", "lastSimulatedAt", "currentLocationId", "currentActivity"
        };
        private static readonly string[] PersonalityFields = { "ambition", "extroversion", "honesty", "riskTolerance", "warmth", "diligence" };
        private static readonly string[] NeedsFields = { "hunger", "energy", "social", "hygiene" };
        private static readonly string[] JobFields = { "title", "workplaceId", "income", "performance" };
        private static readonly string[] ScheduleBlockFields = { "start", "end", "activity", "locationId" };
        private static readonly string[] GoalsFields = { "shortTerm", "longTerm" };
        private static readonly string[] RelationshipFields = { "affinity", "trust", "familiarity", "lastInteractionAt", "type" };
        private static readonly string[] MemoryFactFields = { "timestamp", "eventType", "description", "importanceScore", "involvedNpcIds" };
        private static readonly string[] PlayerFields =
        {
            "displayName", "money", "inventoryItemIds", "karma", "notoriety", "gigRating", "completedGigIds", "currentLocationId"
        };

        // \z rather than $: in .NET, $ also matches just before a trailing newline, so "08:30\n" would pass.
        private static readonly Regex ClockTimePattern = new Regex(@"^([01][0-9]|2[0-3]):([0-5][0-9])\z", RegexOptions.CultureInvariant);

        private const string TimestampFormat = "yyyy-MM-dd'T'HH:mm:ss'Z'";

        public static Roster LoadFile(string path, LocationRegistry locations) => Load(File.ReadAllText(path), path, locations);

        public static Roster Load(string json, string sourceName, LocationRegistry locations)
        {
            JToken root = Parse(json, sourceName);
            var reader = new RosterReader(locations);
            Roster roster = reader.ReadRoot(root);
            if (reader.Errors.Count > 0) throw new RosterLoadException(sourceName, reader.Errors);
            return roster;
        }

        private static JToken Parse(string json, string sourceName)
        {
            try
            {
                using (var stringReader = new StringReader(json))
                using (var jsonReader = new JsonTextReader(stringReader))
                {
                    // Keep every string a string. By default Newtonsoft turns anything date-shaped into a DateTime token.
                    jsonReader.DateParseHandling = DateParseHandling.None;
                    jsonReader.FloatParseHandling = FloatParseHandling.Double;

                    JToken root = JToken.ReadFrom(jsonReader, new JsonLoadSettings
                    {
                        // The default, Replace, silently keeps the last of two same-named keys.
                        DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                        LineInfoHandling = LineInfoHandling.Load,
                        CommentHandling = CommentHandling.Ignore
                    });

                    // ReadFrom stops after one value; anything after it would otherwise be ignored.
                    while (jsonReader.Read())
                    {
                        if (jsonReader.TokenType != JsonToken.Comment)
                        {
                            throw new RosterLoadException(sourceName, new[]
                            {
                                new RosterError("$", $"unexpected {jsonReader.TokenType} after the root value", jsonReader.LineNumber, jsonReader.LinePosition)
                            });
                        }
                    }
                    return root;
                }
            }
            catch (JsonReaderException ex)
            {
                throw new RosterLoadException(sourceName, new[] { new RosterError("$", "malformed JSON: " + ex.Message, ex.LineNumber, ex.LinePosition) });
            }
        }

        private sealed class RosterReader
        {
            private readonly LocationRegistry _locations;

            public RosterReader(LocationRegistry locations)
            {
                _locations = locations;
            }

            public List<RosterError> Errors { get; } = new List<RosterError>();

            public Roster ReadRoot(JToken root)
            {
                if (!(root is JObject rootObject))
                {
                    Error("$", root, $"the file must contain a JSON object, found {Describe(root)}");
                    return null;
                }

                foreach (JProperty property in rootObject.Properties())
                {
                    // README convention: top-level keys starting with '$' are documentation, not data.
                    if (property.Name.StartsWith("$", StringComparison.Ordinal)) continue;
                    if (property.Name != "npcs" && property.Name != "player")
                        Error(property.Name, property, "unknown top-level field (allowed: npcs, player, and $-prefixed documentation keys)");
                }

                List<NPC> npcs = null;
                if (!rootObject.TryGetValue("npcs", out JToken npcsToken))
                    Error("npcs", rootObject, "missing required field");
                else if (npcsToken is JArray npcArray)
                    npcs = ReadNpcs(npcArray);
                else
                    Error("npcs", npcsToken, $"must be an array, found {Describe(npcsToken)}");

                PlayerCharacter player = null;
                if (rootObject.TryGetValue("player", out JToken playerToken))
                    player = ReadPlayer(playerToken);

                return Errors.Count == 0 ? new Roster(npcs, player) : null;
            }

            private List<NPC> ReadNpcs(JArray array)
            {
                var read = new List<(NPC Npc, string Path, JObject Source)>();
                var firstPathById = new Dictionary<string, string>(StringComparer.Ordinal);

                for (int i = 0; i < array.Count; i++)
                {
                    if (!(array[i] is JObject source))
                    {
                        Error($"npcs[{i}]", array[i], $"must be an object, found {Describe(array[i])}");
                        continue;
                    }

                    string displayId = source["id"]?.Type == JTokenType.String ? (string)source["id"] : null;
                    string path = string.IsNullOrWhiteSpace(displayId) ? $"npcs[{i}]" : $"npcs[{i}]({displayId})";
                    NPC npc = ReadNpc(source, path);

                    if (npc.Id != null)
                    {
                        if (firstPathById.TryGetValue(npc.Id, out string firstPath))
                            Error($"{path}.id", source["id"], $"duplicate NPC id '{npc.Id}', already used at {firstPath}");
                        else
                            firstPathById.Add(npc.Id, path);
                    }
                    read.Add((npc, path, source));
                }

                // References can point forward in the file, so they're checked once every id is known.
                foreach (var entry in read)
                    CheckNpcReferences(entry.Npc, entry.Path, entry.Source, firstPathById);

                return read.Select(entry => entry.Npc).ToList();
            }

            private NPC ReadNpc(JObject obj, string path)
            {
                CheckUnknownFields(obj, path, NpcFields);
                return new NPC
                {
                    Id = ReadString(obj, path, "id"),
                    DisplayName = ReadString(obj, path, "displayName"),
                    Age = ReadInt(obj, path, "age", 0),
                    AppearanceRef = ReadString(obj, path, "appearanceRef"),
                    Personality = ReadPersonality(obj, path),
                    Needs = ReadNeeds(obj, path),
                    Job = ReadJob(obj, path),
                    HomeLocationId = ReadHome(obj, path),
                    InventoryItemIds = ReadStringList(obj, path, "inventoryItemIds"),
                    Money = ReadInt(obj, path, "money", 0),
                    Schedule = ReadSchedule(obj, path),
                    Goals = ReadGoals(obj, path),
                    Relationships = ReadRelationships(obj, path),
                    MemoryStream = ReadMemoryStream(obj, path),
                    CurrentTier = ReadEnum<SimTier>(obj, path, "currentTier"),
                    LastSimulatedAt = ReadLastSimulatedAt(obj, path),
                    CurrentLocationId = ReadLocationId(obj, path, "currentLocationId"),
                    CurrentActivity = ReadString(obj, path, "currentActivity"),
                };
            }

            private Personality ReadPersonality(JObject parent, string path)
            {
                JObject obj = ReadObject(parent, path, "personality", PersonalityFields, out string p);
                if (obj == null) return null;
                return new Personality
                {
                    Ambition = ReadFloat(obj, p, "ambition", 0f, 1f),
                    Extroversion = ReadFloat(obj, p, "extroversion", 0f, 1f),
                    Honesty = ReadFloat(obj, p, "honesty", 0f, 1f),
                    RiskTolerance = ReadFloat(obj, p, "riskTolerance", 0f, 1f),
                    Warmth = ReadFloat(obj, p, "warmth", 0f, 1f),
                    Diligence = ReadFloat(obj, p, "diligence", 0f, 1f),
                };
            }

            private Needs ReadNeeds(JObject parent, string path)
            {
                JObject obj = ReadObject(parent, path, "needs", NeedsFields, out string p);
                if (obj == null) return null;
                return new Needs
                {
                    Hunger = ReadFloat(obj, p, "hunger", 0f, 100f),
                    Energy = ReadFloat(obj, p, "energy", 0f, 100f),
                    Social = ReadFloat(obj, p, "social", 0f, 100f),
                    Hygiene = ReadFloat(obj, p, "hygiene", 0f, 100f),
                };
            }

            private Job ReadJob(JObject parent, string path)
            {
                if (!TryGetRequired(parent, path, "job", out JToken token)) return null;

                // The key is required, but null is a real value: an unemployed NPC (§7).
                if (token.Type == JTokenType.Null) return null;

                string p = $"{path}.job";
                if (!(token is JObject obj))
                {
                    Error(p, token, $"must be an object or null, found {Describe(token)}");
                    return null;
                }
                CheckUnknownFields(obj, p, JobFields);
                return new Job
                {
                    Title = ReadString(obj, p, "title"),
                    WorkplaceId = ReadLocationId(obj, p, "workplaceId"),
                    Income = ReadInt(obj, p, "income", 0),
                    Performance = ReadFloat(obj, p, "performance", 0f, 1f),
                };
            }

            private string ReadHome(JObject obj, string path)
            {
                string id = ReadString(obj, path, "homeLocationId");
                if (id == null) return null;

                Location home = CheckLocation(id, $"{path}.homeLocationId", obj["homeLocationId"]);
                // Sleep and Bathe are only feasible at a residence, so a non-residence home would rule them out forever.
                if (home != null && !home.IsResidence)
                    Error($"{path}.homeLocationId", obj["homeLocationId"], $"'{id}' exists but is not a residence");
                return id;
            }

            private List<ScheduleBlock> ReadSchedule(JObject parent, string path)
            {
                JArray array = ReadArray(parent, path, "schedule", out string p);
                if (array == null) return null;

                var blocks = new List<ScheduleBlock>(array.Count);
                for (int i = 0; i < array.Count; i++)
                {
                    string bp = $"{p}[{i}]";
                    if (!(array[i] is JObject obj))
                    {
                        Error(bp, array[i], $"must be an object, found {Describe(array[i])}");
                        continue;
                    }
                    CheckUnknownFields(obj, bp, ScheduleBlockFields);

                    TimeSpan? start = ReadClockTime(obj, bp, "start");
                    TimeSpan? end = ReadClockTime(obj, bp, "end");
                    // An end earlier than start wraps midnight, so start == end would read as a 24-hour block.
                    if (start.HasValue && end.HasValue && start.Value == end.Value)
                        Error($"{bp}.end", obj["end"], $"equals start ({start.Value:hh\\:mm}); a block can't start and end at the same time");

                    blocks.Add(new ScheduleBlock
                    {
                        Start = start ?? default,
                        End = end ?? default,
                        Activity = ReadString(obj, bp, "activity"),
                        LocationId = ReadLocationId(obj, bp, "locationId"),
                    });
                }
                return blocks;
            }

            private Goals ReadGoals(JObject parent, string path)
            {
                JObject obj = ReadObject(parent, path, "goals", GoalsFields, out string p);
                if (obj == null) return null;
                return new Goals
                {
                    ShortTerm = ReadStringList(obj, p, "shortTerm"),
                    LongTerm = ReadStringList(obj, p, "longTerm"),
                };
            }

            private Dictionary<string, Relationship> ReadRelationships(JObject parent, string path)
            {
                if (!TryGetRequired(parent, path, "relationships", out JToken token)) return null;
                string p = $"{path}.relationships";
                if (!(token is JObject obj))
                {
                    Error(p, token, $"must be an object keyed by NPC id, found {Describe(token)}");
                    return null;
                }

                var relationships = new Dictionary<string, Relationship>(StringComparer.Ordinal);
                foreach (JProperty property in obj.Properties())
                {
                    string rp = $"{p}.{property.Name}";
                    if (!(property.Value is JObject rel))
                    {
                        Error(rp, property.Value, $"must be an object, found {Describe(property.Value)}");
                        continue;
                    }
                    CheckUnknownFields(rel, rp, RelationshipFields);
                    relationships[property.Name] = new Relationship
                    {
                        Affinity = ReadFloat(rel, rp, "affinity", -1f, 1f),
                        Trust = ReadFloat(rel, rp, "trust", 0f, 1f),
                        Familiarity = ReadFloat(rel, rp, "familiarity", 0f, 1f),
                        LastInteractionAt = ReadTimestamp(rel, rp, "lastInteractionAt"),
                        Type = ReadEnum<RelationshipType>(rel, rp, "type"),
                    };
                }
                return relationships;
            }

            private List<MemoryFact> ReadMemoryStream(JObject parent, string path)
            {
                JArray array = ReadArray(parent, path, "memoryStream", out string p);
                if (array == null) return null;

                var facts = new List<MemoryFact>(array.Count);
                for (int i = 0; i < array.Count; i++)
                {
                    string fp = $"{p}[{i}]";
                    if (!(array[i] is JObject obj))
                    {
                        Error(fp, array[i], $"must be an object, found {Describe(array[i])}");
                        continue;
                    }
                    CheckUnknownFields(obj, fp, MemoryFactFields);
                    facts.Add(new MemoryFact
                    {
                        Timestamp = ReadTimestamp(obj, fp, "timestamp"),
                        EventType = ReadString(obj, fp, "eventType"),
                        Description = ReadString(obj, fp, "description"),
                        ImportanceScore = ReadFloat(obj, fp, "importanceScore", 0f, 1f),
                        InvolvedNpcIds = ReadStringList(obj, fp, "involvedNpcIds"),
                    });
                }
                return facts;
            }

            private void CheckNpcReferences(NPC npc, string path, JObject source, IReadOnlyDictionary<string, string> knownIds)
            {
                if (source["relationships"] is JObject relationships)
                {
                    foreach (JProperty property in relationships.Properties())
                    {
                        string rp = $"{path}.relationships.{property.Name}";
                        if (property.Name == npc.Id)
                            Error(rp, property, "an NPC can't have a relationship with itself");
                        else if (!knownIds.ContainsKey(property.Name))
                            Error(rp, property, $"relationship target '{property.Name}' is not an NPC id in this roster");
                    }
                }

                if (source["memoryStream"] is JArray memories)
                {
                    for (int i = 0; i < memories.Count; i++)
                    {
                        if (!(memories[i] is JObject fact) || !(fact["involvedNpcIds"] is JArray ids)) continue;
                        for (int j = 0; j < ids.Count; j++)
                        {
                            if (ids[j].Type != JTokenType.String) continue;
                            string id = (string)ids[j];
                            if (!string.IsNullOrWhiteSpace(id) && !knownIds.ContainsKey(id))
                                Error($"{path}.memoryStream[{i}].involvedNpcIds[{j}]", ids[j], $"'{id}' is not an NPC id in this roster");
                        }
                    }
                }
            }

            private PlayerCharacter ReadPlayer(JToken token)
            {
                const string p = "player";
                if (!(token is JObject obj))
                {
                    Error(p, token, $"must be an object, found {Describe(token)}");
                    return null;
                }
                CheckUnknownFields(obj, p, PlayerFields);
                return new PlayerCharacter
                {
                    DisplayName = ReadString(obj, p, "displayName"),
                    Money = ReadInt(obj, p, "money", 0),
                    InventoryItemIds = ReadStringList(obj, p, "inventoryItemIds"),
                    Karma = ReadFloat(obj, p, "karma", -1f, 1f),
                    Notoriety = ReadFloat(obj, p, "notoriety", 0f, 1f),
                    GigRating = ReadFloat(obj, p, "gigRating", 0f, 5f),
                    CompletedGigIds = ReadStringList(obj, p, "completedGigIds"),
                    CurrentLocationId = ReadLocationId(obj, p, "currentLocationId"),
                };
            }

            // ---- Field readers. Each reports its own problems; a returned placeholder never escapes Load. ----

            private void CheckUnknownFields(JObject obj, string path, string[] allowed)
            {
                foreach (JProperty property in obj.Properties())
                {
                    if (Array.IndexOf(allowed, property.Name) < 0)
                        Error($"{path}.{property.Name}", property, $"unknown field (allowed: {string.Join(", ", allowed)})");
                }
            }

            private bool TryGetRequired(JObject obj, string path, string name, out JToken token)
            {
                if (obj.TryGetValue(name, out token)) return true;
                Error($"{path}.{name}", obj, "missing required field");
                return false;
            }

            private JObject ReadObject(JObject parent, string path, string name, string[] allowedFields, out string fieldPath)
            {
                fieldPath = $"{path}.{name}";
                if (!TryGetRequired(parent, path, name, out JToken token)) return null;
                if (!(token is JObject obj))
                {
                    Error(fieldPath, token, $"must be an object, found {Describe(token)}");
                    return null;
                }
                CheckUnknownFields(obj, fieldPath, allowedFields);
                return obj;
            }

            private JArray ReadArray(JObject parent, string path, string name, out string fieldPath)
            {
                fieldPath = $"{path}.{name}";
                if (!TryGetRequired(parent, path, name, out JToken token)) return null;
                if (token is JArray array) return array;
                Error(fieldPath, token, $"must be an array, found {Describe(token)}");
                return null;
            }

            private string ReadString(JObject obj, string path, string name) =>
                TryGetRequired(obj, path, name, out JToken token) ? AsNonEmptyString(token, $"{path}.{name}") : null;

            private string AsNonEmptyString(JToken token, string path)
            {
                if (token.Type != JTokenType.String)
                {
                    Error(path, token, $"must be a string, found {Describe(token)}");
                    return null;
                }
                string value = (string)token;
                if (string.IsNullOrWhiteSpace(value))
                {
                    Error(path, token, "must be a non-empty string");
                    return null;
                }
                return value;
            }

            private List<string> ReadStringList(JObject obj, string path, string name)
            {
                JArray array = ReadArray(obj, path, name, out string p);
                if (array == null) return null;

                var list = new List<string>(array.Count);
                for (int i = 0; i < array.Count; i++)
                {
                    string value = AsNonEmptyString(array[i], $"{p}[{i}]");
                    if (value != null) list.Add(value);
                }
                return list;
            }

            private string ReadLocationId(JObject obj, string path, string name)
            {
                string id = ReadString(obj, path, name);
                if (id != null) CheckLocation(id, $"{path}.{name}", obj[name]);
                return id;
            }

            private Location CheckLocation(string id, string path, JToken token)
            {
                if (_locations.TryGet(id, out Location location)) return location;
                string known = string.Join(", ", _locations.All.Select(l => l.Id).OrderBy(x => x, StringComparer.Ordinal));
                Error(path, token, $"unknown location id '{id}' (known: {known})");
                return null;
            }

            private int ReadInt(JObject obj, string path, string name, int min)
            {
                if (!TryGetRequired(obj, path, name, out JToken token)) return 0;
                string p = $"{path}.{name}";
                if (token.Type != JTokenType.Integer)
                {
                    Error(p, token, $"must be a whole number, found {Describe(token)}");
                    return 0;
                }
                // Integers too big for a long arrive as BigInteger, which fails the pattern and is reported.
                if (!(((JValue)token).Value is long value) || value < min || value > int.MaxValue)
                {
                    Error(p, token, $"must be a whole number from {min} to {int.MaxValue}, found {token.ToString(Formatting.None)}");
                    return 0;
                }
                return (int)value;
            }

            private float ReadFloat(JObject obj, string path, string name, float min, float max)
            {
                if (!TryGetRequired(obj, path, name, out JToken token)) return 0f;
                string p = $"{path}.{name}";
                if (token.Type != JTokenType.Float && token.Type != JTokenType.Integer)
                {
                    Error(p, token, $"must be a number, found {Describe(token)}");
                    return 0f;
                }

                double raw = (double)token;
                float value = (float)raw;
                // NaN fails every comparison, so a plain range check would let it straight through.
                if (double.IsNaN(raw) || double.IsInfinity(raw) || float.IsInfinity(value) || value < min || value > max)
                {
                    Error(p, token, $"must be a number from {FormatBound(min)} to {FormatBound(max)}, found {token.ToString(Formatting.None)}");
                    return 0f;
                }
                return value;
            }

            // Enum.TryParse would accept numeric strings like "1", so only exact, case-sensitive names are valid.
            private TEnum ReadEnum<TEnum>(JObject obj, string path, string name) where TEnum : struct, Enum
            {
                if (!TryGetRequired(obj, path, name, out JToken token)) return default;
                string[] names = Enum.GetNames(typeof(TEnum));
                if (token.Type == JTokenType.String && names.Contains((string)token, StringComparer.Ordinal))
                    return (TEnum)Enum.Parse(typeof(TEnum), (string)token);

                Error($"{path}.{name}", token, $"must be one of [{string.Join(", ", names)}], found {Describe(token)}");
                return default;
            }

            private DateTime ReadTimestamp(JObject obj, string path, string name)
            {
                TryReadTimestamp(obj, path, name, out DateTime value);
                return value;
            }

            private bool TryReadTimestamp(JObject obj, string path, string name, out DateTime value)
            {
                value = default;
                if (!TryGetRequired(obj, path, name, out JToken token)) return false;
                if (token.Type == JTokenType.String &&
                    DateTime.TryParseExact((string)token, TimestampFormat, CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value))
                {
                    return true;
                }
                Error($"{path}.{name}", token, $"must be an ISO-8601 UTC timestamp like 2026-09-12T18:30:00Z, found {Describe(token)}");
                return false;
            }

            private DateTime ReadLastSimulatedAt(JObject obj, string path)
            {
                if (!TryReadTimestamp(obj, path, "lastSimulatedAt", out DateTime value)) return value;
                if (value < GameTime.GameEpoch)
                {
                    Error($"{path}.lastSimulatedAt", obj["lastSimulatedAt"],
                        $"is before the game clock's epoch ({GameTime.GameEpoch:yyyy-MM-ddTHH:mm:ssZ}), so it can't be converted to game time");
                }
                return value;
            }

            private TimeSpan? ReadClockTime(JObject obj, string path, string name)
            {
                if (!TryGetRequired(obj, path, name, out JToken token)) return null;
                if (token.Type == JTokenType.String)
                {
                    Match match = ClockTimePattern.Match((string)token);
                    if (match.Success)
                    {
                        return new TimeSpan(
                            int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture),
                            int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture),
                            0);
                    }
                }
                Error($"{path}.{name}", token, $"must be a 24-hour HH:MM time like 08:30, found {Describe(token)}");
                return null;
            }

            private void Error(string path, JToken token, string message)
            {
                var lineInfo = (IJsonLineInfo)token;
                Errors.Add(lineInfo != null && lineInfo.HasLineInfo()
                    ? new RosterError(path, message, lineInfo.LineNumber, lineInfo.LinePosition)
                    : new RosterError(path, message, null, null));
            }

            private static string Describe(JToken token)
            {
                if (token.Type == JTokenType.Null) return "null";
                string text = token.ToString(Formatting.None);
                if (text.Length > 40) text = text.Substring(0, 37) + "...";
                return $"{token.Type.ToString().ToLowerInvariant()} {text}";
            }

            private static string FormatBound(float bound) => bound.ToString("0.##", CultureInfo.InvariantCulture);
        }
    }
}
