// SPDX-License-Identifier: MIT
// PGO profile collection: runs every workload scenario through ProfilingDecoder
// on a fresh ArduinoUnoSimulation, applies declared stimuli through the TestKit,
// and attributes cycles/counts back to MIR blocks, edges, loops and functions.

using AVR8Sharp.Core;
using AVR8Sharp.Core.Decoders;
using AVR8Sharp.Core.Peripherals;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.AVR.Profiler;

public static class PgoRunner
{
    private const ushort BreakOpcode = 0x9598;
    private const double Hcsr04UsPerCm = 0.017;

    public static PgoProfile Run(string hexContent, WorkloadFile workload,
        BlockMapFile blockmap, uint freq, string chip)
    {
        // Resolved block boundaries sorted by word address. Entries the linker
        // dropped (peephole-deleted labels) have no WordAddr and cannot be
        // attributed -- their PCs merge into the preceding block at runtime.
        var blocks = blockmap.Blocks
            .Where(b => b.WordAddr.HasValue)
            .OrderBy(b => b.WordAddr!.Value)
            .ToList();
        var starts = blocks.Select(b => (long)b.WordAddr!.Value).ToArray();

        var profile = new PgoProfile { Chip = chip, Freq = freq };
        var acc = new Accumulator();
        foreach (var sc in workload.Scenarios)
            RunScenario(hexContent, sc, blocks, starts, freq, profile, acc);
        return profile;
    }

    private sealed class Accumulator
    {
        public readonly Dictionary<string, ulong> BlockCount = new();
        public readonly Dictionary<string, ulong> BlockCycles = new();
        public readonly Dictionary<string, ulong> FuncCycles = new();
        public readonly Dictionary<string, ulong> FuncEntries = new();
        public readonly Dictionary<string, long> Edges = new();
        // loopHeader -> (iterations via back-edges, entries via forward/outside edges)
        public readonly Dictionary<string, (ulong Iter, ulong Entries)> Loops = new();
    }

    private sealed class TimedEvent
    {
        public ulong NextCycle;
        public ulong? EveryCycles;
        public WorkloadStimulus Stim = new();
        public bool Toggling;
        public bool Level = true;
    }

    private sealed class Hcsr04Responder
    {
        public required AvrIoPort TrigPort;
        public required int TrigPin;
        public required AvrIoPort EchoPort;
        public required int EchoPin;
        public ulong DelayCycles;
        public ulong PulseCycles;
        public int Phase;               // 0 idle, 1 wait delay, 2 echo high, 3 wait trig low
        public ulong T0;
    }

    private static void RunScenario(string hexContent, WorkloadScenario sc,
        List<BlockMapBlock> blocks, long[] starts, uint freq,
        PgoProfile profile, Accumulator acc)
    {
        var sim = new ArduinoUnoSimulation();
        sim.WithHex(hexContent);
        var cpu = sim.Cpu;
        double cyclesPerUs = freq / 1_000_000.0;

        // ── Stimulus schedule ────────────────────────────────────────────────
        var events = new List<TimedEvent>();
        var responders = new List<Hcsr04Responder>();
        var warnings = new List<string>();
        foreach (var stim in sc.Stimuli)
        {
            if (stim.Responder != null)
            {
                if (stim.Responder != "hc_sr04")
                {
                    warnings.Add($"unknown responder '{stim.Responder}' (skipped)");
                    continue;
                }
                var trig = ParsePin(stim.Trig);
                var echo = ParsePin(stim.Echo);
                if (trig == null || echo == null)
                {
                    warnings.Add("hc_sr04 responder needs trig/echo pins like 'PD9'/'PD8' (skipped)");
                    continue;
                }
                responders.Add(new Hcsr04Responder
                {
                    TrigPort = PortOf(sim, trig.Value.Port),
                    TrigPin = trig.Value.Pin,
                    EchoPort = PortOf(sim, echo.Value.Port),
                    EchoPin = echo.Value.Pin,
                    DelayCycles = (ulong)((stim.EchoDelayUs ?? 450.0) * cyclesPerUs),
                    PulseCycles = (ulong)Math.Max(1, (stim.DistanceCm ?? 10.0) / Hcsr04UsPerCm * cyclesPerUs),
                });
                continue;
            }
            if (stim.AtUs == null && stim.EveryUs == null)
            {
                warnings.Add("stimulus without at_us/every_us (skipped)");
                continue;
            }
            events.Add(new TimedEvent
            {
                NextCycle = (ulong)((stim.AtUs ?? stim.EveryUs ?? 0) * cyclesPerUs),
                EveryCycles = stim.EveryUs.HasValue ? (ulong)(stim.EveryUs.Value * cyclesPerUs) : null,
                Stim = stim,
            });
        }
        events.Sort((a, b) => a.NextCycle.CompareTo(b.NextCycle));

        // ── Run bound ────────────────────────────────────────────────────────
        ulong hardCapCycles = (ulong)((sc.Run.MaxMs ?? 5000.0) / 1000.0 * freq);
        ulong? budget = sc.Run.Cycles.HasValue ? (ulong)sc.Run.Cycles.Value
            : sc.Run.Ms.HasValue ? (ulong)(sc.Run.Ms.Value / 1000.0 * freq)
            : null;
        if (budget.HasValue && budget.Value < hardCapCycles) hardCapCycles = budget.Value;
        bool untilBreak = sc.Run.Until == "break";
        int? untilUart = sc.Run.UntilUartBytes;

        ulong instructions = 0;
        string? prevBlock = null;
        string? prevFunc = null;
        long prevAddr = -1;
        long prevPc = -1;
        ulong prevCycles = cpu.Cycles;
        bool done = false;
        string? crash = null;

        void ApplyStimulus(WorkloadStimulus s, TimedEvent ev)
        {
            if (s.Pin != null)
            {
                var pin = ParsePin(s.Pin);
                if (pin == null) return;
                var port = PortOf(sim, pin.Value.Port);
                if (ev.Toggling)
                {
                    ev.Level = !ev.Level;
                    port.SetPinValue((byte)pin.Value.Pin, ev.Level);
                }
                else if (s.Toggle == true)
                {
                    ev.Toggling = true;
                    port.SetPinValue((byte)pin.Value.Pin, ev.Level);
                }
                else
                {
                    port.SetPinValue((byte)pin.Value.Pin, (s.Level ?? 1) != 0);
                }
            }
            if (s.UartRx != null)
                foreach (var b in s.UartRx)
                    sim.Serial.InjectByte((byte)b);
        }

        void TickResponders(ulong cycles)
        {
            foreach (var r in responders)
            {
                bool trigHigh = r.TrigPort.GetPinState((byte)r.TrigPin) == PinState.High;
                switch (r.Phase)
                {
                    case 0:
                        if (trigHigh) { r.T0 = cycles; r.Phase = 1; }
                        break;
                    case 1:
                        if (cycles - r.T0 >= r.DelayCycles)
                        {
                            r.EchoPort.SetPinValue((byte)r.EchoPin, true);
                            r.T0 = cycles;
                            r.Phase = 2;
                        }
                        break;
                    case 2:
                        if (cycles - r.T0 >= r.PulseCycles)
                        {
                            r.EchoPort.SetPinValue((byte)r.EchoPin, false);
                            r.Phase = 3;
                        }
                        break;
                    case 3:
                        if (!trigHigh) r.Phase = 0;
                        break;
                }
            }
        }

        void OnInstruction(uint pc, ulong cycles)
        {
            instructions++;
            var delta = cycles - prevCycles;
            prevCycles = cycles;

            int bi = BlockIndex(starts, pc);
            string? curBlock = bi >= 0 ? blocks[bi].Label : null;
            string? curFunc = bi >= 0 ? blocks[bi].Function : null;

            if (prevBlock != null)
            {
                acc.BlockCycles[prevBlock] = acc.BlockCycles.GetValueOrDefault(prevBlock) + delta;
                acc.FuncCycles[prevFunc!] = acc.FuncCycles.GetValueOrDefault(prevFunc!) + delta;
            }
            if (curBlock != null)
            {
                acc.BlockCount[curBlock] = acc.BlockCount.GetValueOrDefault(curBlock) + 1;
                if (prevBlock != null && curBlock != prevBlock)
                {
                    var key = prevBlock + "->" + curBlock;
                    acc.Edges[key] = acc.Edges.GetValueOrDefault(key) + 1;
                    var curAddr = (long)blocks[bi].WordAddr!.Value;
                    if (curFunc == prevFunc && prevAddr >= 0 && curAddr <= prevAddr)
                    {
                        var l = acc.Loops.GetValueOrDefault(curBlock);
                        acc.Loops[curBlock] = (l.Iter + 1, l.Entries);
                    }
                }
                else if (curBlock != null && curBlock == prevBlock && (long)pc < prevPc)
                {
                    // Single-block loop: `L_35: ... BRNE L_35` never leaves its
                    // block, so only a backward PC inside it reveals the back-edge.
                    var l = acc.Loops.GetValueOrDefault(curBlock);
                    acc.Loops[curBlock] = (l.Iter + 1, l.Entries);
                }
                if (curBlock != prevBlock && blocks[bi].Entry && curFunc != prevFunc)
                    acc.FuncEntries[curFunc!] = acc.FuncEntries.GetValueOrDefault(curFunc!) + 1;
            }

            prevBlock = curBlock ?? prevBlock;
            prevFunc = curFunc ?? prevFunc;
            prevAddr = bi >= 0 ? (long)blocks[bi].WordAddr!.Value : prevAddr;
            prevPc = pc;

            while (events.Count > 0 && events[0].NextCycle <= cycles)
            {
                var ev = events[0];
                ApplyStimulus(ev.Stim, ev);
                if (ev.EveryCycles.HasValue)
                {
                    ev.NextCycle += ev.EveryCycles.Value;
                    events.Sort((a, b) => a.NextCycle.CompareTo(b.NextCycle));
                }
                else
                {
                    events.RemoveAt(0);
                }
            }
            TickResponders(cycles);

            if (untilUart.HasValue && sim.Serial.ByteCount >= untilUart.Value) done = true;
        }

        var decoder = new ProfilingDecoder(OnInstruction);

        try
        {
            while (!done && cpu.Cycles < hardCapCycles)
            {
                if (untilBreak && cpu.Pc < cpu.ProgramMemory.Length
                    && cpu.ProgramMemory[(int)cpu.Pc] == BreakOpcode)
                    break;
                decoder.Decode(cpu);
                cpu.Tick();
            }
        }
        catch (Exception ex)
        {
            crash = ex.Message;
        }

        var result = new PgoScenarioResult
        {
            Name = sc.Name,
            Cycles = cpu.Cycles,
            Instructions = instructions,
            Crashed = crash,
        };
        if (sc.Expect?.UartTx is { } expect)
        {
            var got = sim.Serial.Bytes;
            var want = System.Text.Encoding.UTF8.GetBytes(expect);
            result.ExpectMet = got.Length >= want.Length
                && got.AsSpan(0, want.Length).SequenceEqual(want);
            result.ExpectDetail = result.ExpectMet == true
                ? null
                : $"expected uart_tx prefix '{expect}', got '{System.Text.Encoding.UTF8.GetString(got)[..Math.Min(got.Length, 80)]}'";
        }
        foreach (var w in warnings)
            Console.Error.WriteLine($"[workload:{sc.Name}] {w}");
        profile.Scenarios.Add(result);

        profile.Blocks = acc.BlockCount.ToDictionary(
            kv => kv.Key,
            kv => new PgoBlockStat { Count = kv.Value, Cycles = acc.BlockCycles.GetValueOrDefault(kv.Key) });
        profile.Functions = acc.FuncCycles.Keys.Union(acc.FuncEntries.Keys).ToDictionary(
            k => k,
            k => new PgoFunctionStat { Cycles = acc.FuncCycles.GetValueOrDefault(k), Entries = acc.FuncEntries.GetValueOrDefault(k) });
        profile.Edges = acc.Edges;
        // Loop entries = edges into the header from a different block. A block
        // entered only from itself still iterated, but it was never re-entered.
        profile.Loops = acc.Loops.ToDictionary(
            kv => kv.Key,
            kv => new PgoLoopStat
            {
                Iterations = kv.Value.Iter,
                Entries = acc.Edges
                    .Where(e => e.Key.EndsWith("->" + kv.Key) && !e.Key.StartsWith(kv.Key + "->"))
                    .Aggregate(0UL, (a, e) => a + (ulong)e.Value),
            });
    }

    private static int BlockIndex(long[] starts, uint pc)
    {
        int lo = 0, hi = starts.Length - 1, ans = -1;
        while (lo <= hi)
        {
            int mid = (lo + hi) / 2;
            if (starts[mid] <= pc) { ans = mid; lo = mid + 1; }
            else hi = mid - 1;
        }
        return ans;
    }

    private static (char Port, int Pin)? ParsePin(string? name)
    {
        if (string.IsNullOrEmpty(name) || name.Length < 3) return null;
        if (name[0] != 'P' && name[0] != 'p') return null;
        char port = char.ToUpperInvariant(name[1]);
        if (port is not ('B' or 'C' or 'D')) return null;
        if (!int.TryParse(name[2..], out int pin) || pin is < 0 or > 7) return null;
        return (port, pin);
    }

    private static AvrIoPort PortOf(ArduinoUnoSimulation sim, char port) => port switch
    {
        'B' => sim.PortB,
        'C' => sim.PortC,
        'D' => sim.PortD,
        _ => throw new ArgumentOutOfRangeException(nameof(port)),
    };
}
