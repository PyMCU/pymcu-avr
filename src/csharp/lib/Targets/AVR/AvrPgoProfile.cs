// SPDX-License-Identifier: MIT
// The PGO profile consumed by `pymcuc-avr --profile` (see RFC 0010 in the
// pymcu repo). Only the per-block execution counts are read, keyed by the MIR
// label names this backend emitted into the block map; scenarios, edges,
// loops and function stats are inputs for other consumers and are ignored
// here.

using System.Text.Json;
using System.Text.Json.Serialization;
using PyMCU.IR;

namespace PyMCU.Backend.Targets.AVR;

public sealed class AvrPgoProfile
{
    [JsonPropertyName("format")] public int Format { get; set; }
    [JsonPropertyName("blocks")] public Dictionary<string, AvrPgoBlockStat> Blocks { get; set; } = new();

    /// <summary>
    /// Load a profile JSON file, or return null when it cannot be read/parsed.
    /// Never a hard error: a bad profile degrades to an ordinary build.
    /// </summary>
    public static AvrPgoProfile? Load(string path)
    {
        try
        {
            using var stream = File.OpenRead(path);
            var p = JsonSerializer.Deserialize(stream, AvrPgoProfileJsonContext.Default.AvrPgoProfile);
            return p is { Format: 1 } ? p : null;
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>
    /// Whether any profiled block key names a label/function this program
    /// actually contains. A profile captured from another program (or from a
    /// build whose labels all shifted) is worse than no profile -- warn and
    /// ignore it, the same rule pymcuc applies.
    /// </summary>
    public bool MatchesProgram(ProgramIR program)
    {
        if (Blocks.Count == 0) return false;
        var names = new HashSet<string>(program.Functions.Select(f => f.Name));
        foreach (var f in program.Functions)
            foreach (var ins in f.Body)
                if (ins is Label l) names.Add(l.Name);
        return Blocks.Keys.Any(names.Contains);
    }
}

public sealed class AvrPgoBlockStat
{
    [JsonPropertyName("count")] public ulong Count { get; set; }
    [JsonPropertyName("cycles")] public ulong Cycles { get; set; }
}

[JsonSerializable(typeof(AvrPgoProfile))]
internal partial class AvrPgoProfileJsonContext : JsonSerializerContext { }
