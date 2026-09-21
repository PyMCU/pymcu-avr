// SPDX-License-Identifier: MIT
// pymcuc-avr-pgo-survey, symbol table + static region classification.
//
// Input: firmware.symbols.json produced from `avr-nm --format=bsd` on
// dist/debug/firmware.elf, same shape profile.py writes: a list of
// {"Name": ..., "WordAddr": ...} for every .text symbol (t/T), plus an optional
// "dataSymbols" list for SRAM (a/b/d) names used by the variable analysis.

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PyMCU.AVR.PgoSurvey;

public sealed record SymbolRec(string Name, int WordAddr);

public sealed class SymbolTable
{
    // All text symbols sorted by word address, includes inner-function labels
    // (L_*, _dly_*, *.L*) so that delay-loop landmarks resolve precisely.
    public readonly (int Addr, string Name)[] All;

    // Function-entry symbols only: local labels removed. Used for
    // "containing function" attribution and the never-entered list.
    public readonly (int Addr, string Name)[] Functions;

    // SRAM symbols from nm a/b/d records (absolute + bss + data).
    public readonly (int Addr, string Name)[] DataSymbols;

    // Local label: L_*, _x_y internal helpers (_dly_L0, _dly_S2, _systick_*),
    // and *.L<n> sub-labels inside outlined subroutines.
    private static bool IsLocalLabel(string name) =>
        name.StartsWith("L_")
        || Regex.IsMatch(name, @"^_[^_]+_.+")
        || Regex.IsMatch(name, @"\.L\d+$");

    public SymbolTable(IEnumerable<(string name, int addr)> text,
                       IEnumerable<(string name, int addr)> data)
    {
        All = text.Select(t => (t.addr, t.name)).OrderBy(t => t.addr).ToArray();
        Functions = All.Where(t => !IsLocalLabel(t.Name)).ToArray();
        DataSymbols = data.Select(t => (t.addr, t.name)).OrderBy(t => t.addr).ToArray();
    }

    /// <summary>Nearest symbol ≤ pc in the full (labels included) map.</summary>
    public string Resolve(int wordAddr) => ResolveIn(All, wordAddr);

    /// <summary>Nearest FUNCTION symbol ≤ pc (labels skipped).</summary>
    public string ContainingFunction(int wordAddr) => ResolveIn(Functions, wordAddr);

    private static string ResolveIn((int Addr, string Name)[] map, int wordAddr)
    {
        int lo = 0, hi = map.Length - 1, best = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) >> 1;
            if (map[mid].Addr <= wordAddr) { best = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        return best >= 0 ? map[best].Name : $"[0x{wordAddr:X4}]";
    }

    /// <summary>Word address of a symbol, or -1.</summary>
    public int AddrOf(string name)
    {
        foreach (var s in All) if (s.Name == name) return s.Addr;
        return -1;
    }

    public int NextFuncAddrAfter(int wordAddr)
    {
        foreach (var s in Functions) if (s.Addr > wordAddr) return s.Addr;
        return int.MaxValue;
    }

    public static SymbolTable Load(string path)
    {
        var root = JsonNode.Parse(File.ReadAllText(path));
        List<Entry> text = [], data = [];
        if (root is JsonArray arr)
        {
            // profile.py shape: a bare list of {Name, WordAddr} text symbols.
            foreach (var n in arr)
                text.Add(new Entry { Name = n!["Name"]!.GetValue<string>(),
                                     WordAddr = n["WordAddr"]!.GetValue<int>() });
        }
        else
        {
            var doc = root!.Deserialize<SymbolsFile>() ?? new SymbolsFile();
            text = doc.Symbols ?? []; data = doc.DataSymbols ?? [];
        }
        return new SymbolTable(text.Select(e => (e.Name, e.WordAddr)),
                               data.Select(e => (e.Name, e.WordAddr)));
    }

    public sealed class SymbolsFile
    {
        public List<Entry>? Symbols { get; set; }
        public List<Entry>? DataSymbols { get; set; }
    }
    public sealed class Entry
    {
        public string Name { get; set; } = "";
        public int WordAddr { get; set; }
    }
}
