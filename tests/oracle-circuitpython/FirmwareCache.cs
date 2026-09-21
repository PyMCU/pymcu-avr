using System.Security.Cryptography;

namespace PyMCU.OracleCircuitPython;

/// <summary>
/// Downloads the pinned CircuitPython 10.3.1 UF2 for the Raspberry Pi Pico and
/// the 2 MiB flash snapshot built on top of it, under a cache directory that
/// survives between runs: <c>$PYMCU_ORACLE_CACHE</c>, else
/// <c>$XDG_CACHE_HOME/pymcu-oracle</c>, else <c>~/.cache/pymcu-oracle</c>.
///
/// The UF2 is verified by MD5 on every use -- a cached or freshly-downloaded
/// file whose hash does not match the pin is deleted and a hard error raised,
/// never a silent boot of the wrong bytes. Nothing here is committed.
/// </summary>
internal static class FirmwareCache
{
    public const string Version = "10.3.1";

    public const string Uf2FileName =
        "adafruit-circuitpython-raspberry_pi_pico-en_US-10.3.1.uf2";

    /// <summary>MD5 of the pinned UF2, measured once against the released file.</summary>
    public const string Uf2Md5 = "327b526a211c5d78151f9aa3e119e550";

    private const string Uf2Url =
        "https://downloads.circuitpython.org/bin/raspberry_pi_pico/en_US/" + Uf2FileName;

    public static string CacheDir =>
        Environment.GetEnvironmentVariable("PYMCU_ORACLE_CACHE")
        ?? Path.Combine(
            Environment.GetEnvironmentVariable("XDG_CACHE_HOME")
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache"),
            "pymcu-oracle");

    public static string Uf2Path => Path.Combine(CacheDir, Uf2FileName);
    public static string SnapshotPath => Path.Combine(CacheDir, "cp-libs-flash.bin");
    public static string SnapshotStampPath => Path.Combine(CacheDir, "cp-libs-flash.stamp");

    /// <summary>
    /// Local path of the pinned UF2, downloading it if absent. Throws
    /// <see cref="InvalidOperationException"/> on a hash mismatch or when the
    /// download fails (the tests then fail loudly -- there is no offline skip:
    /// the oracle is meaningless if it does not run).
    /// </summary>
    public static async Task<string> GetUf2Async()
    {
        Directory.CreateDirectory(CacheDir);
        if (File.Exists(Uf2Path) && new FileInfo(Uf2Path).Length > 0)
            return VerifyOrThrow(Uf2Path);

        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("pymcu-avr-oracle-circuitpython/1.0");
            var bytes = await http.GetByteArrayAsync(Uf2Url);
            await File.WriteAllBytesAsync(Uf2Path, bytes);
        }
        catch (Exception ex)
        {
            if (File.Exists(Uf2Path)) File.Delete(Uf2Path);
            throw new InvalidOperationException(
                $"Cannot download {Uf2FileName} from {Uf2Url}: {ex.Message}. " +
                $"Pre-seed the cache at {Uf2Path} to run offline.", ex);
        }
        return VerifyOrThrow(Uf2Path);
    }

    private static string VerifyOrThrow(string path)
    {
        using var stream = File.OpenRead(path);
        var actual = Convert.ToHexStringLower(MD5.HashData(stream));
        if (!actual.Equals(Uf2Md5, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(path);
            throw new InvalidOperationException(
                $"CircuitPython UF2 hash mismatch: expected MD5 {Uf2Md5}, got {actual}. " +
                "Re-pin the hash after reviewing the new build (deleted the bad cache copy).");
        }
        return path;
    }
}
