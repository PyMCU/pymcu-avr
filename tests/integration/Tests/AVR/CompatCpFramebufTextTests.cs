// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;
using AVR8Sharp.Core.Peripherals;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// RFC 0008 demandant (docs/rfcs/0008-embedded-files.md, Section 7): the
/// unmodified Adafruit SSD1306 driver renders <c>display.text("PyMCU", 0, 0, 1)</c>
/// -- which reaches <c>adafruit_framebuf.BitmapFont</c> and its
/// <c>open("font5x8.bin", "rb")</c> -- then <c>display.show()</c> writes the
/// 128x32 MONO_VLSB framebuffer over I2C. The font is an embedded romfs blob
/// (1282 bytes): open() resolves at compile time, and every seek/read lands on
/// flash through the same machinery const tables use.
///
/// Same oracle shape as <see cref="AdafruitSsd1306UnmodifiedTests"/>: the CPython
/// side (oracle/oracle.py, a copy of that fixture's oracle) runs the identical
/// vendored sources plus the identical font file against a fake I2C bus and
/// prints every transaction; the test records the emulated Uno's TWI traffic and
/// requires the two streams to be byte-identical. The last transaction is the
/// show(): control byte 0x40 followed by 512 framebuffer bytes whose first 30
/// columns carry the glyphs -- P=127,9,9,9,6 y=76,144,144,144,124
/// M=127,2,28,2,127 C=62,65,65,65,34 U=63,64,64,64,63, one blank column each.
/// </summary>
[TestFixture]
public class CompatCpFramebufTextTests
{
    private const string Fixture = "compat-cp-framebuf-text";
    private const byte OledAddr = 0x3C;

    /// <summary>One I2C transaction: slave address, direction, data bytes (SLA excluded).</summary>
    private sealed record Transaction(byte Address, bool IsWrite, byte[] Data);

    /// <summary>
    /// The recorded stream plus, when the run stopped abnormally, a description
    /// of the stop -- a firmware crash before or mid-stream is a divergence too,
    /// and it must be reported, not thrown through the runner.
    /// </summary>
    private sealed record RunResult(List<Transaction> Transactions, string? Crash);

    // md5 of each vendored file, pinned so an accidental edit fails the test.
    private static readonly (string RelPath, string Md5)[] VendoredFiles =
    {
        ("src/adafruit_ssd1306.py",              "d70a9d7eb565ebf6f4ff595ee9076956"), // Adafruit_CircuitPython_SSD1306 tag 2.12.24
        ("src/adafruit_framebuf.py",             "f8a793f779537106702c42cb8569dcd1"), // PyPI adafruit-circuitpython-framebuf 1.6.12
        ("src/adafruit_bus_device/__init__.py",  "d41d8cd98f00b204e9800998ecf8427e"),
        ("src/adafruit_bus_device/i2c_device.py","2451d2fdd1ea8bf925976763e7781b4f"),
        ("src/adafruit_bus_device/spi_device.py","d78273b262908517d1425d0a5961dd21"),
        ("src/font5x8.bin",                      "221fa943a9d845f68ab2155c75d644fe"), // Adafruit_CircuitPython_framebuf font5x8.bin
    };

    private static string _hex = null!;
    private static string _pyHex = null!;
    private static List<Transaction> _oracle = null!;

    [OneTimeSetUp]
    public void BuildFirmwareAndRunOracle()
    {
        _hex    = PymcuCompiler.BuildFixture(Fixture);
        _pyHex  = PymcuCompiler.BuildFixturePyParser(Fixture);
        _oracle = RunOracle();
    }

    // ── Tests ───────────────────────────────────────────────────────────────

    [Test]
    public void VendoredFiles_AreByteIdenticalToUpstream()
    {
        var root = PymcuCompiler.FixtureDir(Fixture);
        foreach (var (rel, expectedMd5) in VendoredFiles)
        {
            var path = Path.Combine(root, rel.Replace('/', Path.DirectorySeparatorChar));
            File.Exists(path).Should().BeTrue($"vendored file {rel} must exist in the fixture");
            var actual = Convert.ToHexString(MD5.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
            actual.Should().Be(expectedMd5,
                $"{rel} must stay byte-identical to upstream, or the oracle no longer measures the unmodified driver");
        }
    }

    [Test]
    public void Text_Show_SendsTheSameI2cTrafficAsCPython()
    {
        var run = RecordTransactions(_hex);
        AssertTransactionsEqual(_oracle, run, "C# front end");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        var run = RecordTransactions(_pyHex);
        AssertTransactionsEqual(_oracle, run, "Python front end");
    }

    [Test]
    public void BothFrontEnds_ProduceIdenticalFirmware()
    {
        _pyHex.Should().Be(_hex,
            "both front ends must compile the same sources to byte-identical firmware");
    }

    // ── Oracle ──────────────────────────────────────────────────────────────

    private static List<Transaction> RunOracle()
    {
        var script = Path.Combine(PymcuCompiler.FixtureDir(Fixture), "oracle", "oracle.py");
        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(PymcuCompiler.Root, ".venv", "bin", "python"),
            Arguments = script,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start oracle python process.");
        var stdoutTask = Task.Run(() => proc.StandardOutput.ReadToEnd());
        var stderrTask = Task.Run(() => proc.StandardError.ReadToEnd());
        if (!proc.WaitForExit(60_000))
        {
            proc.Kill(entireProcessTree: true);
            throw new TimeoutException("oracle.py did not finish within 60 s");
        }
        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException(
                $"oracle.py failed (exit {proc.ExitCode}):\n{stdout}\n{stderr}");

        return stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                     .Select(ParseLine)
                     .ToList();
    }

    // Line formats: "<addr hex> <data hex>" for a write (data may be absent for
    // the zero-length probe) and "<addr hex> R <data hex>" for a read.
    private static Transaction ParseLine(string line)
    {
        var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var addr = byte.Parse(parts[0], NumberStyles.HexNumber);
        var isRead = parts.Length > 1 && parts[1] == "R";
        var dataHex = isRead
            ? (parts.Length > 2 ? parts[2] : "")
            : (parts.Length > 1 ? parts[1] : "");
        return new Transaction(addr, !isRead, Convert.FromHexString(dataHex));
    }

    // ── Emulator ────────────────────────────────────────────────────────────

    private static RunResult RecordTransactions(string hex)
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.AddTwi(AvrTwi.TwiConfig, out var twi);
        var recorder = new TransactionRecorder(twi, OledAddr);
        twi.EventHandler = recorder;

        string? crash = null;
        try
        {
            // The program is the probe, the init burst, one text() (which opens,
            // seeks and reads the embedded font) and one show() of 513 bytes at
            // 100 kHz -- well under 500 ms of wire time. Stop as soon as the
            // oracle's count of transactions has been seen, then a short tail to
            // catch anything the firmware emits beyond it.
            uno.RunUntilMs(_ => recorder.Transactions.Count >= _oracle.Count, maxMs: 2000);
            uno.RunMilliseconds(50);
        }
        catch (Exception ex)
        {
            crash =
                $"simulation stopped abnormally: {ex.GetType().Name}: {ex.Message} " +
                $"at PC=0x{uno.Cpu.Pc:X4} (byte 0x{uno.Cpu.Pc * 2:X5}), " +
                $"SP=0x{uno.Cpu.Sp:X4}, SREG=0x{uno.Cpu.Sreg:X2}, " +
                $"cycles={uno.Cpu.Cycles}; {recorder.Transactions.Count} " +
                $"TWI transactions recorded before the stop";
            recorder.Flush();
        }
        return new RunResult(recorder.Transactions, crash);
    }

    /// <summary>ACKs address 0x3C only; records transactions with boundaries.</summary>
    private sealed class TransactionRecorder(AvrTwi twi, byte address) : ITwiEventHandler
    {
        private readonly List<byte> _current = [];
        private byte _addr;
        private bool _write;
        private bool _open;

        public List<Transaction> Transactions { get; } = [];

        public void Start(bool repeated) => twi.CompleteStart();

        public void Stop()
        {
            Close();
            twi.CompleteStop();
        }

        public void ConnectToSlave(byte addr, bool write)
        {
            Close();  // a repeated START closes the previous transaction
            _addr = addr;
            _write = write;
            _current.Clear();
            _open = true;
            twi.CompleteConnect(addr == address);
        }

        public void WriteByte(byte data)
        {
            _current.Add(data);
            twi.CompleteWrite(true);
        }

        public void ReadByte(bool ack)
        {
            _current.Add(0xFF);
            twi.CompleteRead(0xFF);
        }

        /// <summary>Closes a transaction still open when the run stops (no STOP seen).</summary>
        public void Flush() => Close();

        private void Close()
        {
            if (!_open) return;
            Transactions.Add(new Transaction(_addr, _write, _current.ToArray()));
            _open = false;
        }
    }

    // ── Comparison ──────────────────────────────────────────────────────────

    private static void AssertTransactionsEqual(
        List<Transaction> expected, RunResult run, string frontEnd)
    {
        var actual = run.Transactions;
        var crashNote = run.Crash != null ? $" [{run.Crash}]" : "";
        var common = Math.Min(expected.Count, actual.Count);
        for (var i = 0; i < common; i++)
        {
            var e = expected[i];
            var a = actual[i];
            if (e.Address != a.Address || e.IsWrite != a.IsWrite)
            {
                Assert.Fail(
                    $"[{frontEnd}] first differing transaction index {i}: " +
                    $"expected addr 0x{e.Address:X2} {Dir(e.IsWrite)}, " +
                    $"actual addr 0x{a.Address:X2} {Dir(a.IsWrite)}{crashNote}");
            }

            var limit = Math.Min(e.Data.Length, a.Data.Length);
            for (var b = 0; b < limit; b++)
            {
                if (e.Data[b] == a.Data[b]) continue;
                Assert.Fail(
                    $"[{frontEnd}] transaction {i} (addr 0x{e.Address:X2} {Dir(e.IsWrite)}, " +
                    $"{e.Data.Length} bytes) first differs at byte offset {b}: " +
                    $"expected 0x{e.Data[b]:X2}, actual 0x{a.Data[b]:X2}{crashNote}\n" +
                    $"expected window: {Window(e.Data, b)}\n" +
                    $"actual   window: {Window(a.Data, b)}");
            }

            if (e.Data.Length != a.Data.Length)
            {
                var ev = limit < e.Data.Length ? $"0x{e.Data[limit]:X2}" : "(absent)";
                var av = limit < a.Data.Length ? $"0x{a.Data[limit]:X2}" : "(absent)";
                Assert.Fail(
                    $"[{frontEnd}] transaction {i} (addr 0x{e.Address:X2} {Dir(e.IsWrite)}) " +
                    $"length differs: expected {e.Data.Length} bytes, actual {a.Data.Length} bytes; " +
                    $"common prefix equal, first differing byte offset {limit}: " +
                    $"expected {ev}, actual {av}{crashNote}\n" +
                    $"expected window: {Window(e.Data, limit)}\n" +
                    $"actual   window: {Window(a.Data, limit)}");
            }
        }

        if (expected.Count != actual.Count)
        {
            var extra = expected.Count < actual.Count;
            var tx = extra ? actual[common] : expected[common];
            Assert.Fail(
                $"[{frontEnd}] transaction count differs: expected {expected.Count}, " +
                $"actual {actual.Count}; first {(extra ? "extra" : "missing")} transaction " +
                $"at index {common} (addr 0x{tx.Address:X2} {Dir(tx.IsWrite)}, " +
                $"{tx.Data.Length} bytes){crashNote}");
        }
    }

    private static string Dir(bool isWrite) => isWrite ? "write" : "read";

    /// <summary>Short hex dump around <paramref name="offset"/>: never dumps a whole buffer.</summary>
    private static string Window(byte[] data, int offset, int radius = 8)
    {
        var lo = Math.Max(0, offset - radius);
        var hi = Math.Min(data.Length, offset + radius + 1);
        if (lo >= hi) return "(empty)";
        var sb = new StringBuilder();
        sb.Append($"[{lo}..{hi - 1}]");
        for (var i = lo; i < hi; i++)
            sb.Append(i == offset ? $" >>{data[i]:X2}<<" : $" {data[i]:X2}");
        return sb.ToString();
    }
}
