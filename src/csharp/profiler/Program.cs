// SPDX-License-Identifier: MIT
// pymcuc-avr-profiler — AVR firmware cycle profiler using AVR8Sharp simulation.
//
// Usage:
//   pymcuc-avr-profiler <hex-file> --symbols <path>
//                       [--cycles N | --ms N]  (default: --ms 100)
//                       [--freq HZ]            (default: 16000000)
//                       [--name "label"]
//                       [-o profile.speedscope.json]

using System.CommandLine;
using System.Text.Json;
using PyMCU.AVR.Profiler;

var hexArg = new Argument<string>("hex-file") { Description = "Intel HEX firmware file" };

var symbolsOpt    = new Option<string>("--symbols")       { Description = "Symbols JSON from --emit-symbols" };
var cyclesOpt     = new Option<ulong?>("--cycles")        { Description = "Number of cycles to simulate" };
var msOpt         = new Option<double?>("--ms")           { Description = "Simulated milliseconds (default: 5000)" };
var freqOpt       = new Option<uint>("--freq")            { Description = "Clock frequency Hz", DefaultValueFactory = _ => 16_000_000U };
var nameOpt       = new Option<string>("--name")          { Description = "Profile label", DefaultValueFactory = _ => "firmware (ATmega328P)" };
var outputOpt     = new Option<string>("-o")              { Description = "Output Speedscope JSON path", DefaultValueFactory = _ => "profile.speedscope.json" };
var debugOpt      = new Option<bool>("--debug")           { Description = "Emit call-stack trace to stderr for diagnostics" };
var taskIdAddrOpt = new Option<uint?>("--task-id-addr")   { Description = "SRAM byte address of the current-task-index variable (enables N-task RTOS support)" };
var workloadOpt   = new Option<string?>("--workload")     { Description = "Workload JSON (driver-translated workload.yaml) with scenarios + stimuli" };
var blockmapOpt   = new Option<string?>("--blockmap")     { Description = "Resolved block map JSON from --emit-blockmap" };
var emitProfOpt   = new Option<string?>("--emit-profile") { Description = "Write a PGO profile JSON to this path instead of Speedscope" };
var i2cTraceOpt   = new Option<string?>("--emit-i2c-trace") { Description = "PGO mode: also dump recorded I2C transactions (addr/data/end-cycle) as JSON" };
var chipOpt       = new Option<string>("--chip")          { Description = "Target chip (PGO mode runs on the Uno/ATmega328P model)", DefaultValueFactory = _ => "atmega328p" };

var root = new RootCommand("pymcuc-avr-profiler — AVR firmware cycle profiler");
root.Arguments.Add(hexArg);
root.Options.Add(symbolsOpt);
root.Options.Add(cyclesOpt);
root.Options.Add(msOpt);
root.Options.Add(freqOpt);
root.Options.Add(nameOpt);
root.Options.Add(outputOpt);
root.Options.Add(debugOpt);
root.Options.Add(taskIdAddrOpt);
root.Options.Add(workloadOpt);
root.Options.Add(blockmapOpt);
root.Options.Add(emitProfOpt);
root.Options.Add(i2cTraceOpt);
root.Options.Add(chipOpt);

root.SetAction(pr =>
{
    var hexFile     = pr.GetValue(hexArg)!;
    var symbolsPath = pr.GetValue(symbolsOpt);
    var cycles      = pr.GetValue(cyclesOpt);
    var ms          = pr.GetValue(msOpt);
    var freq        = pr.GetValue(freqOpt);
    var name        = pr.GetValue(nameOpt)!;
    var output      = pr.GetValue(outputOpt)!;
    var debug       = pr.GetValue(debugOpt);
    var taskIdAddr  = pr.GetValue(taskIdAddrOpt);
    var workloadPath = pr.GetValue(workloadOpt);
    var blockmapPath = pr.GetValue(blockmapOpt);
    var emitProfile = pr.GetValue(emitProfOpt);
    var i2cTracePath = pr.GetValue(i2cTraceOpt);
    var chip        = pr.GetValue(chipOpt)!;

    // ── PGO mode: --emit-profile runs the workload scenarios and writes the
    // block/edge/loop profile the optimizer consumes. ─────────────────────────
    if (emitProfile != null)
    {
        if (blockmapPath == null || workloadPath == null)
        {
            Console.Error.WriteLine("[ERROR] --emit-profile requires --blockmap and --workload");
            Environment.ExitCode = 1;
            return;
        }
        if (!chip.Equals("atmega328p", StringComparison.OrdinalIgnoreCase))
        {
            Console.Error.WriteLine($"[ERROR] PGO profiling runs on the Arduino Uno model (atmega328p); got '{chip}'");
            Environment.ExitCode = 1;
            return;
        }

        string pgoHex;
        WorkloadFile workload;
        BlockMapFile blockmap;
        try
        {
            pgoHex = File.ReadAllText(hexFile);
            workload = JsonSerializer.Deserialize(
                File.ReadAllText(workloadPath), PgoJsonContext.Default.WorkloadFile)
                ?? new WorkloadFile();
            blockmap = JsonSerializer.Deserialize(
                File.ReadAllText(blockmapPath), PgoJsonContext.Default.BlockMapFile)
                ?? new BlockMapFile();
        }
        catch (Exception ex) { Console.Error.WriteLine($"[ERROR] Cannot read inputs: {ex.Message}"); Environment.ExitCode = 1; return; }

        if (workload.Scenarios.Count == 0)
            workload.Scenarios.Add(new WorkloadScenario { Name = "default", Run = new WorkloadRun { Ms = 200 } });

        PgoProfile pgo;
        try { pgo = PgoRunner.Run(pgoHex, workload, blockmap, freq, chip.ToLowerInvariant()); }
        catch (Exception ex) { Console.Error.WriteLine($"[ERROR] Simulation failed: {ex.Message}"); Environment.ExitCode = 1; return; }

        try
        {
            File.WriteAllText(emitProfile,
                JsonSerializer.Serialize(pgo, PgoJsonContext.Default.PgoProfile));
        }
        catch (Exception ex) { Console.Error.WriteLine($"[ERROR] Cannot write profile: {ex.Message}"); Environment.ExitCode = 1; return; }

        if (i2cTracePath != null)
        {
            try
            {
                var trace = pgo.I2cTrace.Select(t => new I2cTraceRow
                {
                    Scenario = t.Scenario, Index = t.Index,
                    Addr = $"0x{t.Addr:X2}", Write = t.Write,
                    Data = Convert.ToHexString(t.Data).ToLowerInvariant(),
                    DataBytes = t.Data.Length, EndCycle = t.EndCycle,
                }).ToList();
                File.WriteAllText(i2cTracePath,
                    JsonSerializer.Serialize(trace, PgoJsonContext.Default.ListI2cTraceRow));
            }
            catch (Exception ex) { Console.Error.WriteLine($"[ERROR] Cannot write i2c trace: {ex.Message}"); Environment.ExitCode = 1; return; }
        }

        var totalCycles = pgo.Scenarios.Aggregate(0UL, (a, s) => a + s.Cycles);
        var failed = pgo.Scenarios.Count(s => s.Crashed != null || s.ExpectMet == false);
        Console.WriteLine($"[PGO] {emitProfile}  ({pgo.Scenarios.Count} scenario(s), {totalCycles:N0} cycles, " +
                          $"{pgo.Blocks.Count} blocks, {pgo.Edges.Count} edges, {pgo.Loops.Count} loops)");
        if (failed > 0)
        {
            Console.Error.WriteLine($"[PGO] {failed} scenario(s) crashed or missed their expectation:");
            foreach (var s in pgo.Scenarios.Where(s => s.Crashed != null || s.ExpectMet == false))
                Console.Error.WriteLine($"  {s.Name}: {(s.Crashed != null ? "crash: " + s.Crashed : "expect: " + s.ExpectDetail)}");
            Environment.ExitCode = 1;
        }
        return;
    }

    if (string.IsNullOrEmpty(symbolsPath))
    {
        Console.Error.WriteLine("[ERROR] --symbols <path> is required");
        Environment.ExitCode = 1;
        return;
    }

    // Resolve simulation length
    ulong cyclesToRun;
    if (cycles.HasValue)
        cyclesToRun = cycles.Value;
    else
    {
        var simMs = ms ?? 5000.0;
        cyclesToRun = (ulong)(simMs / 1000.0 * freq);
    }

    string hexContent;
    try { hexContent = File.ReadAllText(hexFile); }
    catch (Exception ex) { Console.Error.WriteLine($"[ERROR] Cannot read hex: {ex.Message}"); Environment.ExitCode = 1; return; }

    SymbolMap symbols;
    try { symbols = SymbolMap.Load(symbolsPath); }
    catch (Exception ex) { Console.Error.WriteLine($"[ERROR] Cannot read symbols: {ex.Message}"); Environment.ExitCode = 1; return; }

    Console.WriteLine($"[PROFILER] Simulating {cyclesToRun:N0} cycles @ {freq:N0} Hz...");

    SpeedscopeDocument doc;
    try { doc = ProfilerRunner.Run(hexContent, symbols, cyclesToRun, name, debug, taskIdAddr); }
    catch (Exception ex) { Console.Error.WriteLine($"[ERROR] Simulation failed: {ex.Message}"); Environment.ExitCode = 1; return; }

    try { File.WriteAllText(output, doc.ToJson(name, freq)); }
    catch (Exception ex) { Console.Error.WriteLine($"[ERROR] Cannot write output: {ex.Message}"); Environment.ExitCode = 1; return; }

    Console.WriteLine($"[DONE] {output}  ({doc.Samples.Count} samples, {doc.Frames.Count} frames)");
    Console.WriteLine($"       Drag {output} to https://speedscope.app to view the flamegraph.");
});

root.Parse(args).Invoke();
