using System.Security.Cryptography;
using PyMCU.TestKit;
using RP2040.Peripherals;
using RP2040.TestKit.Boards;

namespace PyMCU.OracleCircuitPython;

/// <summary>One autorun on the emulated Pico: the recorded stream plus diagnostics.</summary>
public sealed record CpRun(
    List<I2cTransaction> Transactions,
    int HardwareCount,
    int BitbangCount,
    string ConsoleTail);

/// <summary>
/// Boots real CircuitPython 10.3.1 on RP2040Sharp and drives its CIRCUITPY
/// filesystem over the USB-CDC REPL.
///
/// Two phases, exactly as the research harness that designed this oracle:
///
/// <b>Prepare</b> (once, ~18 s wall): boot the pinned UF2, snapshot the 2 MiB
/// flash, inject a <c>boot.py</c> running <c>storage.disable_usb_drive()</c> so
/// the FAT stays writable despite the USB host, reboot into that image, and
/// install the fixture library files (adafruit_ssd1306, adafruit_framebuf,
/// adafruit_bus_device, font5x8.bin) over the REPL. The result is cached as
/// <c>cp-libs-flash.bin</c> next to the UF2, stamped with a hash of every input
/// so a fixture edit rebuilds it.
///
/// <b>Run</b> (per program, ~1.2 s wall): fresh PicoSimulation on the snapshot,
/// write the transformed <c>code.py</c>, CTRL-D soft reset (boot.py does NOT
/// re-run; the filesystem stays writable), autorun records the wire.
/// </summary>
internal static class CircuitPythonPico
{
    /// <summary>Bump when the install procedure changes; invalidates the snapshot.</summary>
    private const int PrepVersion = 1;

    /// <summary>
    /// The flash snapshot is one per machine, shared by every fixture that
    /// installs the same file set. Lazy so exactly one prep runs even when
    /// NUnit schedules fixtures in parallel.
    /// </summary>
    private static readonly Lazy<Task<byte[]>> Snapshot =
        new(() => PrepareSnapshotAsync());

    public static Task<byte[]> SnapshotAsync() => Snapshot.Value;

    /// <summary>
    /// The one source change the Pico forces: raspberry_pi_pico defines no
    /// default I2C bus, so <c>board.I2C()</c> does not exist there. The fixture's
    /// <c>main.py</c> becomes <c>code.py</c> with the call retargeted to an
    /// explicit bus on the pins the recorder watches (SCL=GP1, SDA=GP0) --
    /// nothing else changes: same driver, same drawing calls.
    /// </summary>
    public static string ToCodePy(string mainPy)
    {
        if (!mainPy.Contains("board.I2C()"))
            throw new InvalidOperationException(
                "fixture main.py no longer calls board.I2C() -- the code.py transform " +
                "has nothing to retarget; review the fixture (and this harness) before trusting a run");
        var code = mainPy.Replace("board.I2C()", "busio.I2C(board.GP1, board.GP0)");
        if (!code.Contains("import busio"))
            code = code.Replace("import board", "import board\nimport busio");
        return code;
    }

    // ── run ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Fresh Pico on the prepared snapshot: write <paramref name="codePy"/> as
    /// <c>code.py</c>, soft-reset to autorun it, and return the recorded stream.
    ///
    /// A program whose tail is <c>while True: pass</c> never returns to the
    /// REPL, so the run cannot wait for the prompt: pass
    /// <paramref name="stopAfterTransactions"/> and the run ends as soon as the
    /// recorder has that many transactions (plus a tail drain to catch strays).
    /// </summary>
    public static CpRun RunAutorun(byte[] snapshot, string codePy, int? stopAfterTransactions = null)
    {
        using var sim = new PicoSimulation(withUsbCdc: true);
        var recorder = new Ssd1306WireRecorder(sim.Rp2040);
        sim.LoadFlash(snapshot);

        if (!WaitForPrompt(sim, 60_000))
            throw new InvalidOperationException(
                "CircuitPython did not reach the REPL on the library snapshot.\n" + Tail(sim));

        var codeBytes = System.Text.Encoding.ASCII.GetBytes(codePy);
        if (!WriteBinary(sim, "code.py", codeBytes))
            throw new InvalidOperationException("writing code.py over the REPL failed.\n" + Tail(sim));

        recorder.Clear();
        sim.UsbCdc.Clear();
        sim.UsbCdc.InjectString("\x04");  // CTRL-D: soft reset -> autorun code.py
        var ran = stopAfterTransactions is int stopAfter
            ? WaitForTransactions(sim, recorder, stopAfter, 300_000)
            : WaitForPrompt(sim, 60_000);
        sim.RunMilliseconds(200);         // drain tail traffic

        var text = sim.UsbCdc.Text;
        var tail = text.Length > 800 ? text[^800..] : text;
        if (!ran || text.Contains("Traceback", StringComparison.Ordinal))
            throw new InvalidOperationException(
                $"code.py did not run clean on CircuitPython (prompt seen: {ran}; " +
                $"{recorder.Transactions.Count} transactions recorded before the stop).\n{tail}");

        return new CpRun(recorder.Transactions, recorder.HwCount, recorder.BitbangCount, tail);
    }

    // ── prepare ─────────────────────────────────────────────────────────────

    private static async Task<byte[]> PrepareSnapshotAsync()
    {
        var uf2Path = await FirmwareCache.GetUf2Async();
        var uf2Bytes = await File.ReadAllBytesAsync(uf2Path);
        var flashImage = RP2040Machine.Uf2ToFlash(uf2Bytes)
            ?? throw new InvalidOperationException($"Could not parse {uf2Path} as a UF2 image.");

        // The files the snapshot installs. Sources are reused in place from the
        // two fixtures; the stamp records their contents so an edit rebuilds.
        var ssdSrc = Repo.FixtureDir("adafruit-ssd1306-unmodified") + "/src";
        var txtSrc = Repo.FixtureDir("compat-cp-framebuf-text") + "/src";
        var files = new (string Src, string Dst)[]
        {
            ($"{ssdSrc}/adafruit_ssd1306.py",              "lib/adafruit_ssd1306.py"),
            ($"{ssdSrc}/adafruit_framebuf.py",             "lib/adafruit_framebuf.py"),
            ($"{ssdSrc}/adafruit_bus_device/__init__.py",  "lib/adafruit_bus_device/__init__.py"),
            ($"{ssdSrc}/adafruit_bus_device/i2c_device.py","lib/adafruit_bus_device/i2c_device.py"),
            ($"{ssdSrc}/adafruit_bus_device/spi_device.py","lib/adafruit_bus_device/spi_device.py"),
            ($"{txtSrc}/font5x8.bin",                      "font5x8.bin"),
        };

        var stamp = Convert.ToHexStringLower(SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(
                PrepVersion + "\n" + FirmwareCache.Uf2Md5 + "\n" +
                string.Join("\n", files.Select(f =>
                    f.Dst + "=" + Convert.ToHexStringLower(
                        SHA256.HashData(File.ReadAllBytes(f.Src))))))));

        if (File.Exists(FirmwareCache.SnapshotPath) &&
            File.Exists(FirmwareCache.SnapshotStampPath) &&
            (await File.ReadAllTextAsync(FirmwareCache.SnapshotStampPath)).Trim() == stamp)
            return await File.ReadAllBytesAsync(FirmwareCache.SnapshotPath);

        // Phase 1: boot once so CircuitPython initialises the FAT, then inject
        // boot.py (disable_usb_drive) into a copy of the flash.
        byte[] writableFlash;
        {
            using var stage1 = new PicoSimulation(withUsbCdc: true);
            stage1.LoadFlash(flashImage);
            if (!WaitForPrompt(stage1, 60_000))
                throw new InvalidOperationException(
                    "CircuitPython phase-1 boot did not reach the REPL.\n" + Tail(stage1));

            writableFlash = ReadFlash(stage1);
            unsafe
            {
                fixed (byte* ptr = writableFlash)
                    InjectBootPy(ptr, "import storage\nstorage.disable_usb_drive()\n");
            }
        }

        // Phase 2: reboot into the writable image and install the libraries.
        byte[] snapshot;
        {
            using var sim = new PicoSimulation(withUsbCdc: true);
            sim.LoadFlash(writableFlash);
            if (!WaitForPrompt(sim, 60_000))
                throw new InvalidOperationException(
                    "CircuitPython writable-fs boot did not reach the REPL.\n" + Tail(sim));
            sim.RunMilliseconds(200);
            sim.UsbCdc.Clear();

            var unhex = "bytes.fromhex";
            if (ExecWait(sim, "import binascii", 10_000))
            {
                ExecWait(sim, "print('HASU' if hasattr(binascii,'unhexlify') else 'NOU')", 10_000);
                if (sim.UsbCdc.Text.Contains("HASU")) unhex = "binascii.unhexlify";
            }
            ExecWait(sim, "import os", 10_000);
            foreach (var d in new[] { "lib", "lib/adafruit_bus_device" })
                ExecWait(sim, $"exec(\"try:\\n os.mkdir('/{d}')\\nexcept OSError:\\n pass\")", 10_000);

            foreach (var (src, dst) in files)
            {
                var data = await File.ReadAllBytesAsync(src);
                if (!WriteBinary(sim, dst, data, unhex))
                    throw new InvalidOperationException(
                        $"installing {dst} over the REPL failed.\n" + Tail(sim));
            }
            sim.RunMilliseconds(200);   // flush the FAT before the snapshot
            snapshot = ReadFlash(sim);
        }

        Directory.CreateDirectory(FirmwareCache.CacheDir);
        await File.WriteAllBytesAsync(FirmwareCache.SnapshotPath, snapshot);
        await File.WriteAllTextAsync(FirmwareCache.SnapshotStampPath, stamp + "\n");
        return snapshot;
    }

    // ── REPL plumbing ────────────────────────────────────────────────────────

    private static bool WaitForPrompt(PicoSimulation sim, double timeoutMs)
    {
        const double batchMs = 100.0;
        var elapsed = 0.0;
        var keySent = false;
        while (elapsed < timeoutMs)
        {
            sim.RunMilliseconds(batchMs);
            elapsed += batchMs;
            if (!keySent && sim.UsbCdc.Text.Contains("Press any key", StringComparison.OrdinalIgnoreCase))
            {
                sim.UsbCdc.InjectString("\r");
                keySent = true;
            }
            if (sim.UsbCdc.Text.Contains(">>> ", StringComparison.Ordinal))
            {
                sim.RunMilliseconds(100);   // drain pending USB endpoint reads
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Waits until the recorder has <paramref name="count"/> transactions, for a
    /// code.py that never returns (ends in <c>while True: pass</c>). Returns
    /// false early if the program raised or fell back to the REPL instead.
    /// </summary>
    private static bool WaitForTransactions(
        PicoSimulation sim, Ssd1306WireRecorder recorder, int count, double timeoutMs)
    {
        const double batchMs = 100.0;
        var elapsed = 0.0;
        while (elapsed < timeoutMs)
        {
            sim.RunMilliseconds(batchMs);
            elapsed += batchMs;
            if (recorder.Transactions.Count >= count) return true;
            var text = sim.UsbCdc.Text;
            if (text.Contains("Traceback", StringComparison.Ordinal) ||
                text.Contains(">>> ", StringComparison.Ordinal))
                return false;
        }
        return false;
    }

    private static bool ExecWait(PicoSimulation sim, string line, double timeoutMs)
    {
        sim.UsbCdc.Clear();
        sim.UsbCdc.InjectString(line + "\r\n");
        var elapsed = 0.0;
        while (elapsed < timeoutMs)
        {
            sim.RunMilliseconds(100);
            if (sim.UsbCdc.Text.Contains(">>> ", StringComparison.Ordinal)) return true;
            elapsed += 100;
        }
        return false;
    }

    /// <summary>Writes a file as hex chunks -- binary-safe for font5x8.bin.</summary>
    private static bool WriteBinary(PicoSimulation sim, string name, byte[] data,
        string unhex = "bytes.fromhex")
    {
        if (!ExecWait(sim, $"_wf=open('/{name}','wb')", 15_000)) return false;
        var hex = Convert.ToHexString(data).ToLowerInvariant();
        const int chunk = 120;
        for (var pos = 0; pos < hex.Length; pos += chunk)
        {
            var part = hex.Substring(pos, Math.Min(chunk, hex.Length - pos));
            if (!ExecWait(sim, $"_wf.write({unhex}('{part}'))", 15_000)) return false;
        }
        return ExecWait(sim, "_wf.close()", 15_000);
    }

    private static string Tail(PicoSimulation sim)
    {
        var t = sim.UsbCdc.Text;
        return t.Length > 800 ? t[^800..] : t;
    }

    private static byte[] ReadFlash(PicoSimulation sim)
    {
        var size = (int)sim.Rp2040.Bus.FlashSize;
        var img = new byte[size];
        unsafe
        {
            new ReadOnlySpan<byte>(sim.Rp2040.Bus.PtrFlash, size).CopyTo(img);
        }
        return img;
    }

    // ── FAT12 boot.py injection ──────────────────────────────────────────────
    //
    // Writes boot.py into the CIRCUITPY FAT12 partition inside a raw flash
    // image (ported from RP2040Sharp's CircuitPythonRunner -- that runner lives
    // in its test assembly, which this project deliberately does not reference).

    private static unsafe void InjectBootPy(byte* flashPtr, string content)
    {
        const uint FatFlashOffset = 0x100000u; // CIRCUITPY FAT partition at 1 MiB in flash
        const int  Bps            = 512;
        const int  Spc            = 1;
        const int  RsvdSectors    = 1;
        const int  NumFats        = 1;
        const int  SectorsPerFat  = 7;
        const int  RootDirEntries = 512;

        const int FatSector  = RsvdSectors;
        const int RootSector = RsvdSectors + NumFats * SectorsPerFat;
        const int DataSector = RootSector  + RootDirEntries * 32 / Bps;

        byte* bpb  = flashPtr + FatFlashOffset;
        byte* fat  = bpb + FatSector  * Bps;
        byte* root = bpb + RootSector * Bps;
        byte* data = bpb + DataSector * Bps;

        int freeClu = -1;
        for (var clu = 2; clu < 2048; clu++)
        {
            if (Fat12Get(fat, clu) == 0x000u) { freeClu = clu; break; }
        }
        if (freeClu < 0)
            throw new InvalidOperationException("CIRCUITPY FAT is full -- cannot inject boot.py");

        var contentBytes = System.Text.Encoding.ASCII.GetBytes(content);
        byte* clusterData = data + (freeClu - 2) * Spc * Bps;
        new Span<byte>(clusterData, Spc * Bps).Clear();
        contentBytes.AsSpan().CopyTo(new Span<byte>(clusterData, contentBytes.Length));

        Fat12Set(fat, freeClu, 0xFFFu);

        for (var e = 0; e < RootDirEntries - 1; e++)
        {
            byte* entry = root + e * 32;
            var isEnd = entry[0] == 0x00;
            var isDeleted = entry[0] == 0xE5;
            if (!isEnd && !isDeleted) continue;

            "BOOT    "u8.CopyTo(new Span<byte>(entry, 8));
            "PY "u8.CopyTo(new Span<byte>(entry + 8, 3));
            entry[11] = 0x20;
            new Span<byte>(entry + 12, 14).Clear();
            *(ushort*)(entry + 26) = (ushort)freeClu;
            *(uint*)(entry + 28) = (uint)contentBytes.Length;
            if (isEnd)
                *(entry + 32) = 0x00;
            break;
        }
    }

    private static unsafe uint Fat12Get(byte* fat, int cluster)
    {
        var byteOff = cluster * 3 / 2;
        var raw = (uint)fat[byteOff] | ((uint)fat[byteOff + 1] << 8);
        return (cluster & 1) == 0 ? raw & 0xFFFu : (raw >> 4) & 0xFFFu;
    }

    private static unsafe void Fat12Set(byte* fat, int cluster, uint value)
    {
        var byteOff = cluster * 3 / 2;
        if ((cluster & 1) == 0)
        {
            fat[byteOff]     = (byte)(value & 0xFF);
            fat[byteOff + 1] = (byte)((fat[byteOff + 1] & 0xF0u) | ((value >> 8) & 0x0Fu));
        }
        else
        {
            fat[byteOff]     = (byte)((fat[byteOff] & 0x0Fu) | ((value << 4) & 0xF0u));
            fat[byteOff + 1] = (byte)((value >> 4) & 0xFF);
        }
    }
}
