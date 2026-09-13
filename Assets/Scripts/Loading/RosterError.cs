using System;
using System.Collections.Generic;
using System.Linq;

namespace SideQuest.Loading
{
    public sealed class RosterError
    {
        public string Path { get; }
        public string Message { get; }
        public int? Line { get; }
        public int? Column { get; }

        public RosterError(string path, string message, int? line, int? column)
        {
            Path = path;
            Message = message;
            Line = line;
            Column = column;
        }

        public override string ToString() =>
            Line.HasValue ? $"{Path}: {Message} (line {Line}, col {Column})" : $"{Path}: {Message}";
    }

    public sealed class RosterLoadException : Exception
    {
        public string SourceName { get; }
        public IReadOnlyList<RosterError> Errors { get; }

        public RosterLoadException(string sourceName, IReadOnlyList<RosterError> errors)
            : base($"Roster '{sourceName}' failed to load with {errors.Count} error(s):{Environment.NewLine}" +
                   string.Join(Environment.NewLine, errors.Select(e => "  - " + e)))
        {
            SourceName = sourceName;
            Errors = errors;
        }
    }
}
