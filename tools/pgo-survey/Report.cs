// SPDX-License-Identifier: MIT
// pymcuc-avr-pgo-survey — post-run analysis and report generation.
//
// Turns the per-PC counters from SurveyRun into the survey.json document:
// bucket shares, hottest symbols, branch/loop statistics, hot-loop memory
// traffic, cold code, stall detection.

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PyMCU.AVR.PgoSurvey;

public sealed class Report
{
    private readonly SurveyRun _r;
    private readonly SymbolTable _syms;
    private readonly Insn[] _meta;
    private readonly int _flashWords;       // words covered by the hex image
    private readonly int _vecEndWord;
    private readonly ushort[] _prog;

    // classification bitmaps over word addresses
    private readonly bool[] _delayW, _uartW, _haltW, _dataW;

    public string Name = "";
    public string DistDir = "";
    public uint FreqHz;
    public long WallMs;
    public ulong CyclesRequested;

    public Report(SurveyRun r, SymbolTable syms, int flashWords)
    {
        _r = r; _syms = syms;
        _prog = r.Cpu.ProgramMemory;
        _meta = r.Meta;
        _flashWords = Math.Min(flashWords, _prog.Length);
        _vecEndWord = 52;   // 26 relaxed vector slots * 2 words on ATmega328P
        _delayW = new bool[_prog.Length];
        _uartW = new bool[_prog.Length];
        _haltW = new bool[_prog.Length];
        _dataW = new bool[_prog.Length];
        Classify();
    }

    // ── Static classification ───────────────────────────────────────────

    private static readonly Regex DlyLabel = new(@"^_dly_L\d+$", RegexOptions.Compiled);

    private void Mark(bool[] map, int lo, int hi)
    {
        lo = Math.Max(0, lo); hi = Math.Min(hi, _prog.Length);
        for (int w = lo; w < hi; w++) map[w] = true;
    }

    private void Classify()
    {
        var all = _syms.All;
        for (int i = 0; i < all.Length; i++)
        {
            var (addr, name) = all[i];
            var next = i + 1 < all.Length ? all[i + 1].Addr : _prog.Length;

            if (name.StartsWith("__flash_") || name.StartsWith("__exn_str_"))
            { Mark(_dataW, addr, next); continue; }

            if (name is "__pymcu_halt_spin" or "__exn_halt" or "_exit" or "__exit")
            { Mark(_haltW, addr, next); continue; }

            // Delay: shared subroutines __dly_cN cover to next symbol; runtime
            // helpers carry "delay"/"_dly" in the mangled name.
            if (name.StartsWith("__dly_c") || name.Contains("delay")
                || name.Contains("_dly_") || name.Contains("__dly")
                || name.Contains("sleep"))
            { Mark(_delayW, addr, next); continue; }

            // Inline const-delay loop: _dly_Ln labels the 5-instruction
            // SUBI/SBCI/BRNE loop, preceded by four LDI setup words.
            if (DlyLabel.IsMatch(name))
            {
                int lo = addr;
                if (addr >= 4)
                {
                    bool ldis = true;
                    for (int w = addr - 4; w < addr; w++)
                        if ((_prog[w] & 0xF000) != 0xE000) { ldis = false; break; }
                    if (ldis) lo = addr - 4;
                }
                Mark(_delayW, lo, addr + 5);
                continue;
            }

            // Print/UART: HAL uart modules, console/strfmt helpers, the
            // unhandled-exception print path.
            if (Regex.IsMatch(name, "uart|console|strfmt|print|stdout|puts",
                    RegexOptions.IgnoreCase)
                || name.StartsWith("__exn_"))
            { Mark(_uartW, addr, next); continue; }

            // Outlined subroutine that stores to UDR0 (0xC6) = UART TX helper.
            if (name.StartsWith("__pymcu_outline_") || name.StartsWith("_pymcu_outline_"))
            {
                for (int w = addr; w < next && w < _prog.Length; w++)
                {
                    var m = _meta[w];
                    if ((m.Kind == InsnKind.Sts && m.Addr == 0xC6))
                    { Mark(_uartW, addr, next); break; }
                }
            }
        }

        // Inline UDRE/RXC poll loops: a backward loop of <= 8 instructions
        // whose body reads UCSR0A (data 0xC0) is UART runtime wherever it sits.
        foreach (var e in _r.Edges)
        {
            int span = e.BranchPc - e.Target + 1;
            if (span > 8) continue;
            bool readsUcsra = false;
            for (int w = e.Target; w <= e.BranchPc; w++)
                if (_meta[w].Kind == InsnKind.Lds && _meta[w].Addr == 0xC0)
                { readsUcsra = true; break; }
            if (readsUcsra) Mark(_uartW, e.Target, e.BranchPc + 1);
        }
    }

    public bool IsDelay(int pc) => _delayW[pc];
    public bool IsUart(int pc) => _uartW[pc];

    // ── Aggregation ─────────────────────────────────────────────────────

    public sealed record FuncStat(string Name, int Addr, long Cyc, long Exec);
    public sealed record EdgeStat(int BranchPc, int Target, string Func,
        long Execs, long Taken, long Entries, long SpanCycles, double Share,
        int Trips, int TripMin, int TripMax, double TripMedian, int TripDistinct,
        bool HasCounter, int SpanInstrs);
    public sealed record StallInfo(bool Stalled, string Kind, int PcLo, int PcHi,
        string Symbol, double TailCover, double TailIsrShare, int TailIoReads,
        bool IoChanged, int[] IoAddrs);

    public List<FuncStat> FuncStats()
    {
        var res = new List<FuncStat>();
        var funcs = _syms.Functions;
        for (int i = 0; i < funcs.Length; i++)
        {
            var (addr, name) = funcs[i];
            var next = i + 1 < funcs.Length ? funcs[i + 1].Addr : _prog.Length;
            long cyc = 0, exec = 0;
            for (int w = addr; w < next && w < _prog.Length; w++)
            { cyc += _r.Cyc[w]; exec += _r.Exec[w]; }
            res.Add(new FuncStat(name, addr, cyc, exec));
        }
        return res;
    }

    public List<EdgeStat> EdgeStats(long totalCyc)
    {
        var res = new List<EdgeStat>();
        foreach (var e in _r.Edges)
        {
            long spanCyc = 0;
            bool counter = false;
            int spanInstrs = 0;
            for (int w = e.Target; w <= e.BranchPc && w < _prog.Length; w++)
            {
                spanCyc += _r.Cyc[w];
                if (Insn.IsCounterOp(_prog[w])) counter = true;
                spanInstrs++;
            }
            var t = e.Trips;
            res.Add(new EdgeStat(
                e.BranchPc, e.Target, _syms.ContainingFunction(e.BranchPc),
                e.Execs, e.Taken, e.Entries, spanCyc,
                totalCyc > 0 ? (double)spanCyc / totalCyc : 0,
                t.Count, t.Count > 0 ? t.Min() : 0, t.Count > 0 ? t.Max() : 0,
                Median(t), t.Distinct().Count(), counter, spanInstrs));
        }
        return res.OrderByDescending(s => s.SpanCycles).ToList();
    }

    private static double Median(List<int> v)
    {
        if (v.Count == 0) return 0;
        var s = v.OrderBy(x => x).ToList();
        return s.Count % 2 == 1 ? s[s.Count / 2] : (s[s.Count / 2 - 1] + s[s.Count / 2]) / 2.0;
    }

    /// <summary>Smallest contiguous word window holding ≥ coverFrac of the
    /// cycles in <paramref name="pcCyc"/>; returns lo,hi,covered.</summary>
    private static (int lo, int hi, long covered) MinWindow(
        Dictionary<int, long> pcCyc, double coverFrac)
    {
        var total = pcCyc.Values.Sum();
        if (total == 0) return (0, 0, 0);
        var need = (long)(total * coverFrac);
        var pcs = pcCyc.Keys.OrderBy(x => x).ToArray();
        int bestLo = 0, bestHi = int.MaxValue;
        for (int i = 0; i < pcs.Length; i++)
        {
            long acc = 0;
            for (int j = i; j < pcs.Length; j++)
            {
                acc += pcCyc[pcs[j]];
                if (acc >= need)
                {
                    if (bestHi == int.MaxValue || pcs[j] - pcs[i] < bestHi - bestLo)
                    { bestLo = pcs[i]; bestHi = pcs[j]; }
                    break;
                }
            }
        }
        if (bestHi == int.MaxValue) return (pcs.First(), pcs.Last(), total);
        long cov = pcCyc.Where(kv => kv.Key >= bestLo && kv.Key <= bestHi)
                        .Sum(kv => kv.Value);
        return (bestLo, bestHi, cov);
    }

    /// <summary>Tail-window stall analysis: last 50% of executed cycles.</summary>
    public StallInfo AnalyzeStall()
    {
        long total = _r.Quanta.Sum(q => q.Cycles);
        if (total == 0 || _r.Quanta.Count == 0)
            return new StallInfo(false, "no-data", 0, 0, "", 0, 0, 0, false, []);

        // Select trailing quanta covering the last 50% of cycles.
        var tailPc = new Dictionary<int, long>();
        long tailIsr = 0, tailCyc = 0;
        var ioVals = new Dictionary<int, HashSet<byte>>();
        int ioReads = 0;
        for (int i = _r.Quanta.Count - 1; i >= 0 && tailCyc < total / 2; i--)
        {
            var q = _r.Quanta[i];
            tailCyc += q.Cycles;
            tailIsr += q.IsrCycles;
            foreach (var kv in q.PcCycles)
                tailPc[kv.Key] = tailPc.GetValueOrDefault(kv.Key) + kv.Value;
            if (q.IoReads != null)
                foreach (var kv in q.IoReads)
                {
                    ioReads += kv.Value.Count;   // distinct vals per quantum (approx)
                    if (!ioVals.TryGetValue(kv.Key, out var s))
                        ioVals[kv.Key] = s = new();
                    foreach (var v in kv.Value) s.Add(v);
                }
        }
        bool ioChanged = ioVals.Values.Any(s => s.Count > 1);
        var (lo, hi, covered) = MinWindow(tailPc, 0.90);
        double cover = tailPc.Values.Sum() > 0
            ? (double)covered / tailPc.Values.Sum() : 0;
        int span = hi - lo + 1;
        bool hasBackEdge = _r.Edges.Any(e => e.Target >= lo && e.BranchPc <= hi)
            || Enumerable.Range(lo, span).Any(w => w < _meta.Length
                && _meta[w].Kind == InsnKind.HaltJump);
        bool windowIsHalt = Enumerable.Range(lo, span)
            .Where(w => w < _meta.Length)
            .All(w => _meta[w].Kind == InsnKind.HaltJump
                      || _haltW[w] || _r.Exec[w] == 0);
        double isrShare = tailCyc > 0 ? (double)tailIsr / tailCyc : 0;
        var sym = _syms.ContainingFunction(lo);

        string kind;
        bool stalled;
        if (span > 8 || !hasBackEdge || cover < 0.90) { stalled = false; kind = "active"; }
        else if (windowIsHalt || InRange(_haltW, lo, hi)) { stalled = false; kind = "halt"; }
        else if (InRange(_delayW, lo, hi)) { stalled = false; kind = "delay-spin"; }
        else if (ioChanged) { stalled = false; kind = "io-progress"; }
        else
        {
            stalled = true;
            kind = ioReads == 0 ? "spin-noio" : "spin-io-wait";
            if (isrShare > 0.05) kind = "spin-isr-active";
        }
        return new StallInfo(stalled, kind, lo, hi, sym, cover, isrShare,
            ioReads, ioChanged, ioVals.Keys.OrderBy(x => x).ToArray());
    }

    private static bool InRange(bool[] map, int lo, int hi)
    {
        for (int w = lo; w <= hi && w < map.Length; w++)
            if (map[w]) return true;
        return false;
    }

    // ── Output document ─────────────────────────────────────────────────

    public object BuildJson()
    {
        var totalCyc = (long)_r.Cpu.Cycles;
        var funcs = FuncStats();
        var top = funcs.OrderByDescending(f => f.Cyc).Take(10).ToList();

        // buckets
        long delay = 0, uart = 0, user = 0;
        for (int w = 0; w < _prog.Length; w++)
        {
            var c = _r.Cyc[w];
            if (c == 0) continue;
            if (_delayW[w]) delay += c;
            else if (_uartW[w]) uart += c;
            else user += c;
        }
        // ISR cycles ride on their own tracking (in-isr flag), not regions:
        // subtract nothing — report both region-based and flag-based shares.
        long bucketSum = delay + uart + user;
        long condTotal = 0, condExec = 0, alwaysTaken = 0, neverTaken = 0,
             mixed = 0, neverExec = 0, takenExecs = 0, notTakenExecs = 0;
        for (int w = 0; w < _flashWords; w++)
        {
            if (!_meta[w].IsCondBranch) continue;
            condTotal++;
            long t = _r.BranchTaken[w], n = _r.BranchNotTaken[w];
            long e = _r.Exec[w];
            if (e == 0) { neverExec++; continue; }
            condExec++;
            takenExecs += t; notTakenExecs += n;
            if (t > 0 && n > 0) mixed++;
            else if (t > 0) alwaysTaken++;
            else neverTaken++;
        }

        // hot-loop selection: greedy cover of ≥80% of cycles by back-edge spans
        var edgeStats = EdgeStats(totalCyc);
        var covered = new bool[_prog.Length];
        long coveredCyc = 0;
        var hotEdges = new List<EdgeStat>();
        foreach (var es in edgeStats)
        {
            if (coveredCyc >= totalCyc * 0.8) break;
            long add = 0;
            for (int w = es.Target; w <= es.BranchPc && w < _prog.Length; w++)
                if (!covered[w]) { covered[w] = true; add += _r.Cyc[w]; }
            if (add == 0 && es.SpanCycles == 0) continue;
            coveredCyc += add;
            hotEdges.Add(es);
        }
        double hotCover = totalCyc > 0 ? (double)coveredCyc / totalCyc : 0;

        // memory traffic inside the hot union
        long ldsSts = 0, ldSt = 0, pushPop = 0, movs = 0, lpm = 0;
        var sram = new HashSet<int>();
        var ioInHot = new HashSet<int>();
        for (int w = 0; w < _prog.Length; w++)
        {
            if (!covered[w] || _r.Exec[w] == 0) continue;
            var m = _meta[w];
            switch (m.Kind)
            {
                case InsnKind.Lds: case InsnKind.Sts: ldsSts += _r.Exec[w]; break;
                case InsnKind.LdX: case InsnKind.LdY: case InsnKind.LdZ:
                case InsnKind.StX: case InsnKind.StY: case InsnKind.StZ:
                    ldSt += _r.Exec[w]; break;
                case InsnKind.Push: case InsnKind.Pop: pushPop += _r.Exec[w]; break;
                case InsnKind.Mov: case InsnKind.Movw: movs += _r.Exec[w]; break;
                case InsnKind.Lpm: lpm += _r.Exec[w]; break;
            }
            if (_r.MemAddrs.TryGetValue(w, out var addrs))
                foreach (var a in addrs)
                    if (a >= 0x100) sram.Add(a); else ioInHot.Add(a);
        }
        long hotMemCyc = 0;   // cycles of LDS/STS/LD/ST/PUSH/POP in hot spans
        for (int w = 0; w < _prog.Length; w++)
        {
            if (!covered[w]) continue;
            if (_meta[w].Kind is InsnKind.Lds or InsnKind.Sts
                or InsnKind.LdX or InsnKind.LdY or InsnKind.LdZ
                or InsnKind.StX or InsnKind.StY or InsnKind.StZ
                or InsnKind.Push or InsnKind.Pop)
                hotMemCyc += _r.Cyc[w];
        }

        // cold code
        int coldBytes = 0, coldOutline = 0, coldInExecFunc = 0, dataBytes = 0;
        var neverEntered = new List<string>();
        for (int i = 0; i < _syms.Functions.Length; i++)
        {
            var f = _syms.Functions[i];
            var next = _syms.NextFuncAddrAfter(f.Addr);
            if (next > _flashWords) next = _flashWords;
            if (f.Addr >= _flashWords) continue;
            if (f.Name.StartsWith("__flash_") || f.Name.StartsWith("__exn_str_"))
            { dataBytes += (Math.Min(next, _flashWords) - f.Addr) * 2; continue; }
            if (_r.Exec[f.Addr] == 0) neverEntered.Add(f.Name);
        }
        for (int w = _vecEndWord; w < _flashWords; w++)
        {
            int head = w, bytes;
            // A 2-word instruction head whose operand word never executed:
            // count the pair together (4 B) and skip the operand word. If the
            // "operand" DID execute, the head word was really data — count
            // 2 B only and let the next word stand on its own.
            if (_meta[w].Words == 2 && w + 1 < _flashWords && _r.Exec[w + 1] == 0)
            { bytes = 4; w++; }
            else bytes = 2;
            if (_dataW[head]) continue;
            if (_r.Exec[head] == 0)
            {
                coldBytes += bytes;
                var fn = _syms.ContainingFunction(head);
                if (fn.StartsWith("__pymcu_outline") || fn.StartsWith("_pymcu_outline"))
                    coldOutline += bytes;
                else if (_r.Exec[_syms.AddrOf(fn)] > 0)
                    coldInExecFunc += bytes;
            }
        }
        int programBytes = (_flashWords - _vecEndWord) * 2;

        // stall
        var stall = AnalyzeStall();

        // loops with small constant trip counts that run a counter op —
        // reroll/unroll candidates — and near-never-run unrolled sequences
        var smallConst = edgeStats.Where(e =>
            e.Trips >= 1 && e.TripMax <= 8 && e.TripDistinct == 1
            && e.HasCounter).ToList();
        var unrolled = DetectUnrolled();

        return new
        {
            name = Name, distDir = DistDir, freqHz = FreqHz,
            cyclesRequested = CyclesRequested,
            cyclesExecuted = _r.Cpu.Cycles,
            instructionsExecuted = _r.Instrs,
            wallMs = WallMs,
            cyclesPerSec = WallMs > 0 ? _r.Cpu.Cycles * 1000.0 / WallMs : 0,
            endReason = _r.EndReason, crash = _r.Crash,
            flashBytes = _flashWords * 2, vectorTableWords = _vecEndWord,
            programBytesExVectors = programBytes,
            buckets = new
            {
                delayCycles = delay, uartCycles = uart, isrCycles = _r.IsrCycles,
                isrEntries = _r.IsrEntries,
                userCycles = user, regionSum = bucketSum,
                delayShare = Pct(delay, totalCyc), uartShare = Pct(uart, totalCyc),
                isrShare = Pct(_r.IsrCycles, totalCyc), userShare = Pct(user, totalCyc),
                note = "isrCycles measured by vector-entry tracking; delay/uart/user by region",
            },
            topSymbols = top.Select(f => new
            { f.Name, addr = f.Addr, cycles = f.Cyc, share = Pct(f.Cyc, totalCyc) }),
            branches = new
            {
                condTotal, condExec, alwaysTaken, neverTaken, mixed, neverExec,
                takenExecs, notTakenExecs,
                takenCycleShare = Pct(takenExecs, totalCyc),
                // per-site detail for every conditional branch in flash
                sites = Enumerable.Range(_vecEndWord, _flashWords - _vecEndWord)
                    .Where(w => _meta[w].IsCondBranch)
                    .Select(w => new
                    {
                        pc = w, func = _syms.ContainingFunction(w),
                        exec = _r.Exec[w],
                        taken = _r.BranchTaken[w], notTaken = _r.BranchNotTaken[w],
                    }),
            },
            // per-PC hit counts (sparse: only words that ever executed)
            pcHits = Enumerable.Range(0, _flashWords)
                .Where(w => _r.Exec[w] > 0)
                .ToDictionary(w => w, w => _r.Exec[w]),
            loops = edgeStats.Select(e => new
            {
                e.BranchPc, e.Target, e.Func, e.Execs, e.Taken, e.Entries,
                e.SpanCycles, e.Share, e.Trips, e.TripMin, e.TripMax,
                e.TripMedian, e.TripDistinct, e.HasCounter, e.SpanInstrs,
                tripHist = _r.Edges.First(x => x.BranchPc == e.BranchPc)
                    .Trips.GroupBy(t => t).OrderBy(g => g.Key)
                    .Take(64).ToDictionary(g => g.Key, g => g.Count()),
            }),
            hot = new
            {
                coverage = hotCover, edges = hotEdges,
                ldsStsExecs = ldsSts, ldStExecs = ldSt, pushPopExecs = pushPop,
                movExecs = movs, lpmExecs = lpm,
                memOpCycles = hotMemCyc,
                distinctSramAddrs = sram.OrderBy(x => x).ToList(),
                sramVarNames = sram.OrderBy(x => x)
                    .Select(a => $"0x{a:X4}:{NameDataSym(a)}").ToList(),
                ioAddrsRead = ioInHot.OrderBy(x => x).ToList(),
            },
            cold = new
            {
                bytes = coldBytes, share = Pct(coldBytes, programBytes),
                inOutlineBytes = coldOutline, inExecutedFuncBytes = coldInExecFunc,
                dataBytes, neverEntered,
            },
            stall,
            loopOpportunity = new
            {
                smallConstTrips = smallConst.Select(e => new
                { e.BranchPc, e.Target, e.Func, e.Trips, e.TripMax, e.Execs, e.Entries }),
                unrolledRare = unrolled,
            },
        };
    }

    private string NameDataSym(int addr)
    {
        string best = "";
        int bestAddr = -1;
        foreach (var d in _syms.DataSymbols)
        {
            if (d.Addr <= addr && d.Addr > bestAddr) { bestAddr = d.Addr; best = d.Name; }
        }
        return best;
    }

    private List<object> DetectUnrolled()
    {
        // A heuristic: ≥3 consecutive repeats of the same 1..4-word pattern,
        // whose entry executed at most once. Unrolled PyMCU loops constant-fold
        // the induction variable, so copies are often word-identical.
        var res = new List<object>();
        for (int k = 1; k <= 4; k++)
        {
            int w = _vecEndWord;
            while (w + 2 * k <= _flashWords)
            {
                int reps = 1;
                while (w + (reps + 1) * k <= _flashWords && reps < 64
                    && SamePattern(w, w + reps * k, k))
                    reps++;
                if (reps >= 3 && _r.Exec[w] <= 1)
                {
                    res.Add(new { pc = w, words = k, reps, execs = _r.Exec[w],
                        func = _syms.ContainingFunction(w) });
                    w += reps * k;
                }
                else w++;
            }
        }
        return res;
    }

    private bool SamePattern(int a, int b, int k)
    {
        for (int i = 0; i < k; i++)
            if (_prog[a + i] != _prog[b + i]) return false;
        return true;
    }

    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    private static double Pct(long part, long total) =>
        total > 0 ? Math.Round((double)part / total * 1000.0) / 1000.0 : 0;

    /// <summary>One aggregate CSV row built from the JSON document itself, so
    /// the columns can never drift from the report.</summary>
    public string CsvLine()
    {
        var j = JsonSerializer.SerializeToNode(BuildJson(), JsonOpts)!;
        string F(string path) => path.Split('.')
            .Aggregate(j, (n, k) => n![k]!)!.ToString();
        var top = j["topSymbols"]!.AsArray();
        var tops = string.Join("|", top.Select(t =>
            $"{t!["name"]}={Math.Round(t["share"]!.GetValue<double>() * 100, 1)}%"));
        var hot = j["hot"]!;
        long ldsSts = hot["ldsStsExecs"]!.GetValue<long>()
                    + hot["ldStExecs"]!.GetValue<long>()
                    + hot["pushPopExecs"]!.GetValue<long>();
        var stall = j["stall"]!;
        var b = j["branches"]!;
        var cold = j["cold"]!;
        var bk = j["buckets"]!;
        var vals = new object[]
        {
            Name,
            j["flashBytes"]!.GetValue<int>(),
            j["cyclesExecuted"]!.GetValue<ulong>(),
            j["instructionsExecuted"]!.GetValue<long>(),
            F("endReason"),
            stall["stalled"]!.GetValue<bool>() ? stall["kind"]!.ToString() : "-",
            bk["delayShare"]!.GetValue<double>(),
            bk["uartShare"]!.GetValue<double>(),
            bk["isrShare"]!.GetValue<double>(),
            bk["userShare"]!.GetValue<double>(),
            tops,
            cold["share"]!.GetValue<double>(),
            b["takenCycleShare"]!.GetValue<double>(),
            ldsSts,
            hot["distinctSramAddrs"]!.AsArray().Count,
            j["wallMs"]!.GetValue<long>(),
        };
        return string.Join(",", vals.Select(v =>
            v.ToString()!.Contains(',') ? $"\"{v}\"" : v.ToString()));
    }

    public static string CsvHeader =>
        "name,flashBytes,cyclesExecuted,instructionsExecuted,endReason," +
        "stall,delayShare,uartShare,isrShare,userShare,topSymbols," +
        "coldShare,takenBranchShare,hotLdsStsExecs,hotSramVars,wallMs";
}
