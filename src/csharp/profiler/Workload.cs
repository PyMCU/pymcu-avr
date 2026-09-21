// SPDX-License-Identifier: MIT
// Workload + PGO profile JSON models for pymcuc-avr-profiler.
// The driver translates workload.yaml into this JSON shape (snake_case keys);
// no YAML parsing happens on the C# side.

using System.Text.Json.Serialization;

namespace PyMCU.AVR.Profiler;

public sealed class WorkloadFile
{
    [JsonPropertyName("scenarios")] public List<WorkloadScenario> Scenarios { get; set; } = new();
}

public sealed class WorkloadScenario
{
    [JsonPropertyName("name")] public string Name { get; set; } = "default";
    [JsonPropertyName("run")] public WorkloadRun Run { get; set; } = new();
    [JsonPropertyName("stimuli")] public List<WorkloadStimulus> Stimuli { get; set; } = new();
    [JsonPropertyName("expect")] public WorkloadExpect? Expect { get; set; }
}

public sealed class WorkloadRun
{
    [JsonPropertyName("ms")] public double? Ms { get; set; }
    [JsonPropertyName("cycles")] public long? Cycles { get; set; }
    [JsonPropertyName("until")] public string? Until { get; set; }          // "break"
    [JsonPropertyName("until_uart_bytes")] public int? UntilUartBytes { get; set; }
    // Stops the run once N I2C transactions have closed (STOP or repeated
    // START), counting only transactions the declared i2c_slave ACKed.
    [JsonPropertyName("until_i2c_transactions")] public int? UntilI2cTransactions { get; set; }
    // Safety cap for until-* runs, milliseconds of simulated time (default 5000).
    [JsonPropertyName("max_ms")] public double? MaxMs { get; set; }
}

public sealed class WorkloadStimulus
{
    [JsonPropertyName("at_us")] public double? AtUs { get; set; }
    [JsonPropertyName("every_us")] public double? EveryUs { get; set; }
    [JsonPropertyName("pin")] public string? Pin { get; set; }              // "PD2"
    [JsonPropertyName("level")] public int? Level { get; set; }
    [JsonPropertyName("toggle")] public bool? Toggle { get; set; }
    [JsonPropertyName("uart_rx")] public List<int>? UartRx { get; set; }    // bytes to inject
    [JsonPropertyName("responder")] public string? Responder { get; set; }  // "hc_sr04"
    [JsonPropertyName("trig")] public string? Trig { get; set; }
    [JsonPropertyName("echo")] public string? Echo { get; set; }
    [JsonPropertyName("distance_cm")] public double? DistanceCm { get; set; }
    [JsonPropertyName("echo_delay_us")] public double? EchoDelayUs { get; set; }
    // 7-bit slave address to ACK on the TWI bus (attaches the TWI peripheral
    // and a device that ACKs this address, returns 0xFF on reads). Untimed:
    // the device is present for the whole scenario.
    [JsonPropertyName("i2c_slave")] public int? I2cSlave { get; set; }
}

public sealed class WorkloadExpect
{
    [JsonPropertyName("uart_tx")] public string? UartTx { get; set; }
    // Prefix over the flattened I2C transaction stream: each transaction an
    // i2c_slave ACKed contributes its address byte then its data bytes.
    [JsonPropertyName("i2c_tx")] public List<int>? I2cTx { get; set; }
}

// ── Block map (produced by pymcuc-avr --emit-blockmap, resolved by the driver) ─

public sealed class BlockMapFile
{
    [JsonPropertyName("Format")] public int Format { get; set; }
    [JsonPropertyName("Blocks")] public List<BlockMapBlock> Blocks { get; set; } = new();
    [JsonPropertyName("Branches")] public List<BlockMapBranch> Branches { get; set; } = new();
}

public sealed class BlockMapBlock
{
    [JsonPropertyName("Function")] public string Function { get; set; } = "";
    [JsonPropertyName("Label")] public string Label { get; set; } = "";
    [JsonPropertyName("Entry")] public bool Entry { get; set; }
    [JsonPropertyName("WordAddr")] public uint? WordAddr { get; set; }
}

public sealed class BlockMapBranch
{
    [JsonPropertyName("Id")] public int Id { get; set; }
    [JsonPropertyName("Function")] public string Function { get; set; } = "";
    [JsonPropertyName("Sym")] public string Sym { get; set; } = "";
    [JsonPropertyName("Taken")] public string Taken { get; set; } = "";
    [JsonPropertyName("Fallthrough")] public string? Fallthrough { get; set; }
    [JsonPropertyName("WordAddr")] public uint? WordAddr { get; set; }
}

// ── PGO profile output ───────────────────────────────────────────────────────

public sealed class PgoProfile
{
    [JsonPropertyName("format")] public int Format { get; set; } = 1;
    [JsonPropertyName("chip")] public string Chip { get; set; } = "";
    [JsonPropertyName("freq")] public uint Freq { get; set; }
    [JsonPropertyName("scenarios")] public List<PgoScenarioResult> Scenarios { get; set; } = new();
    [JsonPropertyName("functions")] public Dictionary<string, PgoFunctionStat> Functions { get; set; } = new();
    [JsonPropertyName("blocks")] public Dictionary<string, PgoBlockStat> Blocks { get; set; } = new();
    [JsonPropertyName("edges")] public Dictionary<string, long> Edges { get; set; } = new();
    [JsonPropertyName("loops")] public Dictionary<string, PgoLoopStat> Loops { get; set; } = new();
    // Recorded I2C transactions across scenarios; written by --emit-i2c-trace,
    // kept out of the profile JSON the compiler consumes.
    [JsonIgnore] public List<PgoRunner.I2cTraceEntry> I2cTrace { get; } = new();
}

public sealed class PgoScenarioResult
{
    [JsonPropertyName("name")] public string Name { get; set; } = "";
    [JsonPropertyName("cycles")] public ulong Cycles { get; set; }
    [JsonPropertyName("instructions")] public ulong Instructions { get; set; }
    [JsonPropertyName("crashed")] public string? Crashed { get; set; }
    [JsonPropertyName("expectMet")] public bool? ExpectMet { get; set; }
    [JsonPropertyName("expectDetail")] public string? ExpectDetail { get; set; }
    [JsonPropertyName("i2cTransactions")] public int? I2cTransactions { get; set; }
}

public sealed class PgoFunctionStat
{
    [JsonPropertyName("cycles")] public ulong Cycles { get; set; }
    [JsonPropertyName("entries")] public ulong Entries { get; set; }
}

public sealed class PgoBlockStat
{
    [JsonPropertyName("count")] public ulong Count { get; set; }
    [JsonPropertyName("cycles")] public ulong Cycles { get; set; }
}

public sealed class PgoLoopStat
{
    [JsonPropertyName("iterations")] public ulong Iterations { get; set; }
    [JsonPropertyName("entries")] public ulong Entries { get; set; }
}

/// <summary>One I2C transaction in the --emit-i2c-trace dump.</summary>
public sealed class I2cTraceRow
{
    [JsonPropertyName("scenario")] public string Scenario { get; set; } = "";
    [JsonPropertyName("index")] public int Index { get; set; }
    [JsonPropertyName("addr")] public string Addr { get; set; } = "";
    [JsonPropertyName("write")] public bool Write { get; set; }
    [JsonPropertyName("data")] public string Data { get; set; } = "";
    [JsonPropertyName("dataBytes")] public int DataBytes { get; set; }
    [JsonPropertyName("endCycle")] public ulong EndCycle { get; set; }
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(WorkloadFile))]
[JsonSerializable(typeof(BlockMapFile))]
[JsonSerializable(typeof(PgoProfile))]
[JsonSerializable(typeof(List<I2cTraceRow>))]
internal partial class PgoJsonContext : JsonSerializerContext { }
