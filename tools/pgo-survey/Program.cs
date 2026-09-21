// SPDX-License-Identifier: MIT
// pymcuc-avr-pgo-survey, profile-guided-optimisation survey harness.
//
//   pgo-survey <dist-dir> --cycles N --freq HZ [--stim] [--bench] [--out FILE]
//
// Loads <dist>/firmware.hex into an ATmega328P (Arduino Uno) simulation,
// executes it under ProfilingDecoder, and writes survey.json + one CSV row.

using System.Diagnostics;
using System.Text.Json;
using AVR8Sharp.Core;
using AVR8Sharp.Core.Decoders;
using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.AVR.PgoSurvey;

public static class Program
{
    public static int Main(string[] args)
    {
        string? dist = null, outPath = null;
        ulong cycles = 0;
        uint freq = 16_000_000;
        bool stim = false, bench = false;
        int benchCycles = 0;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--cycles": cycles = ulong.Parse(args[++i]); break;
                case "--freq":   freq = uint.Parse(args[++i]); break;
                case "--stim":   stim = true; break;
                case "--bench":  bench = true; break;
                case "--bench-cycles": benchCycles = int.Parse(args[++i]); break;
                case "--out":    outPath = args[++i]; break;
                default:
                    if (args[i].StartsWith("--"))
                    { Console.Error.WriteLine($"unknown option {args[i]}"); return 2; }
                    dist = args[i];
                    break;
            }
        }
        if (dist == null || (cycles == 0 && !bench))
        {
            Console.Error.WriteLine(
                "usage: pgo-survey <dist-dir> --cycles N --freq HZ [--stim] [--bench]");
            return 2;
        }

        var hexPath = Path.Combine(dist, "firmware.hex");
        if (!File.Exists(hexPath))
        {
            // debug builds keep artifacts one level down
            hexPath = Path.Combine(dist, "debug", "firmware.hex");
            if (!File.Exists(hexPath))
            { Console.Error.WriteLine($"no firmware.hex under {dist}"); return 2; }
        }
        var hex = File.ReadAllText(hexPath);
        int flashWords = FlashWords(hex);

        var symPath = Path.Combine(dist, "firmware.symbols.json");
        if (!File.Exists(symPath))
            symPath = Path.Combine(dist, "debug", "firmware.symbols.json");
        if (!File.Exists(symPath))
        { Console.Error.WriteLine($"no firmware.symbols.json under {dist}"); return 2; }
        var syms = SymbolTable.Load(symPath);

        if (bench) return RunBench(dist, hex, cycles > 0 ? cycles : (ulong)benchCycles,
                                   freq, syms);

        var sim = new ArduinoUnoSimulation();
        sim.WithHex(hex);
        var run = new SurveyRun(sim, syms);

        var sw = Stopwatch.StartNew();
        run.Run(cycles, freq, stim);
        sw.Stop();

        var rep = new Report(run, syms, flashWords)
        {
            Name = ProjectName(dist),
            DistDir = dist,
            FreqHz = freq,
            WallMs = sw.ElapsedMilliseconds,
            CyclesRequested = cycles,
        };

        var json = JsonSerializer.Serialize(rep.BuildJson(), Report.JsonOpts);
        outPath ??= Path.Combine(dist, "survey.json");
        File.WriteAllText(outPath, json);
        Console.WriteLine(rep.CsvLine());
        Console.Error.WriteLine(
            $"survey: {rep.Name} cycles={run.Cpu.Cycles} instrs={run.Instrs} " +
            $"wall={rep.WallMs}ms end={run.EndReason} -> {outPath}");
        return 0;
    }

    /// <summary>&lt;project&gt;/dist or &lt;project&gt;/dist/debug → the project
    /// directory name.</summary>
    private static string ProjectName(string dist)
    {
        var d = new DirectoryInfo(dist).Parent;
        while (d != null && (d.Name == "debug" || d.Name == "dist"))
            d = d.Parent;
        return d?.Name ?? "unknown";
    }

    /// <summary>Highest word address covered by the hex image +1.</summary>
    private static int FlashWords(string hex)
    {
        int max = 0;
        foreach (var line in hex.Split('\n'))
        {
            var l = line.Trim();
            if (!l.StartsWith(':') || l.Length < 11) continue;
            int count = Convert.ToInt32(l[1..3], 16);
            int addr = Convert.ToInt32(l[3..7], 16);
            int type = Convert.ToInt32(l[7..9], 16);
            if (type == 0) max = Math.Max(max, addr + count);
        }
        return max / 2 + (max % 2);
    }

    // ── Emulator-cost benchmark ─────────────────────────────────────────
    private static int RunBench(string dist, string hex, ulong cycles,
                                uint freq, SymbolTable syms)
    {
        if (cycles == 0) cycles = 3_200_000;   // 200 ms @16 MHz
        Console.Error.WriteLine($"bench: {dist} cycles={cycles}");

        double Median(List<long> v) { v.Sort(); return v[v.Count / 2]; }

        // Warm the decoder/JIT path once so the first timed run isn't cold.
        {
            var warm = new ArduinoUnoSimulation(); warm.WithHex(hex);
            var wrun = new SurveyRun(warm, syms);
            try { warm.RunCyclesProfiled((long)Math.Min(cycles, 400_000),
                new ProfilingDecoder(wrun.OnInstruction)); } catch { }
        }

        var plain = new List<long>(); var profNoop = new List<long>();
        var profCollect = new List<long>();
        for (int i = 0; i < 3; i++)
        {
            var s3 = new ArduinoUnoSimulation(); s3.WithHex(hex);
            var run = new SurveyRun(s3, syms);
            var dec = new ProfilingDecoder(run.OnInstruction);
            var sw = Stopwatch.StartNew();
            try { s3.RunCyclesProfiled((long)cycles, dec); } catch { }
            sw.Stop(); profCollect.Add(sw.ElapsedMilliseconds);

            var s2 = new ArduinoUnoSimulation(); s2.WithHex(hex);
            var noop = new ProfilingDecoder((pc, c) => { });
            sw.Restart();
            try { s2.RunCyclesProfiled((long)cycles, noop); } catch { }
            sw.Stop(); profNoop.Add(sw.ElapsedMilliseconds);

            var s1 = new ArduinoUnoSimulation(); s1.WithHex(hex);
            sw.Restart();
            try { s1.RunCycles((long)cycles); } catch { }
            sw.Stop(); plain.Add(sw.ElapsedMilliseconds);
            GC.Collect();
        }

        var row = new
        {
            name = ProjectName(dist), cycles,
            plainMs = plain, profiledNoopMs = profNoop,
            profiledCollectMs = profCollect,
            medianPlainMs = Median(plain), medianNoopMs = Median(profNoop),
            medianCollectMs = Median(profCollect),
            slowdownNoop = Median(profNoop) / Math.Max(Median(plain), 1),
            slowdownCollect = Median(profCollect) / Math.Max(Median(plain), 1),
            plainCycPerSec = cycles * 1000.0 / Math.Max(Median(plain), 1),
            collectCycPerSec = cycles * 1000.0 / Math.Max(Median(profCollect), 1),
        };
        File.WriteAllText(Path.Combine(dist, "bench.json"),
            JsonSerializer.Serialize(row, Report.JsonOpts));
        Console.WriteLine(JsonSerializer.Serialize(row));
        return 0;
    }
}
