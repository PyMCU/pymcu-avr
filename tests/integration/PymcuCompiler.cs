using System.Collections.Concurrent;
using System.Diagnostics;

namespace PyMCU.IntegrationTests;

/// <summary>
/// Compiles PyMCU AVR firmware using the <c>pymcu build</c> CLI driver and returns
/// the resulting Intel HEX content, ready to load into a simulator.
/// Results are cached in-process so each program is compiled at most once per
/// test session regardless of how many test fixtures reference it.
/// </summary>
public static class PymcuCompiler
{
    private static readonly string RepoRoot = FindRepoRoot();
    private static readonly string PymcuExe = Path.Combine(RepoRoot, ".venv", "bin", "pymcu");

    // Bound only the compile step. NUnit runs fixtures in parallel (= ProcessorCount
    // threads); each cold fixture spawns pymcu -> pymcuc -> pymcuc-avr -> avra, so without
    // a cap a high-core machine can launch dozens of toolchain processes at once and OOM.
    // The simulation phase (the slow part of each test) stays fully parallel -- this gate
    // is released as soon as the firmware is built. Builds are cached (Lazy below), so each
    // fixture passes through here at most once. ProcessorCount scales with the host; the
    // floor keeps tiny CI runners moving and the ceiling protects 16/32-core machines.
    private static readonly SemaphoreSlim BuildGate = new(Math.Clamp(Environment.ProcessorCount, 2, 8));

    // Lazy<T> guarantees the factory runs at most once even if multiple threads
    // race on the same key -- ConcurrentDictionary.GetOrAdd(key, factory) can
    // invoke the factory more than once, so we wrap the result in Lazy.
    private static readonly ConcurrentDictionary<string, Lazy<string>> Cache = new();

    /// <summary>
    /// Compiles the showcase example at <c>examples/{name}</c>.
    /// </summary>
    /// <param name="name">Example directory name, e.g. <c>"blink"</c>.</param>
    public static string Build(string name)
        => Cache.GetOrAdd("ex:" + name,
            _ => new Lazy<string>(() => Compile(Path.Combine(RepoRoot, "examples", name), name))).Value;

    /// <summary>
    /// Compiles the compiler test fixture at <c>tests/integration/fixtures/{name}</c>.
    /// </summary>
    /// <param name="name">Fixture directory name, e.g. <c>"bitwise-ops"</c>.</param>
    public static string BuildFixture(string name)
        => Cache.GetOrAdd("fx:" + name,
            _ => new Lazy<string>(() => Compile(Path.Combine(RepoRoot, "tests", "integration", "fixtures", name), name))).Value;

    // ── Internal ─────────────────────────────────────────────────────────────

    private static string Compile(string exampleDir, string name)
    {
        BuildGate.Wait();
        try { return CompileImpl(exampleDir, name); }
        finally { BuildGate.Release(); }
    }

    private static string CompileImpl(string exampleDir, string name)
    {
        Console.WriteLine($"[PymcuCompiler] RepoRoot    : {RepoRoot}");
        Console.WriteLine($"[PymcuCompiler] PymcuExe    : {PymcuExe} (exists={File.Exists(PymcuExe)})");
        Console.WriteLine($"[PymcuCompiler] ExampleDir  : {exampleDir} (exists={Directory.Exists(exampleDir)})");
        Console.WriteLine($"[PymcuCompiler] PATH        : {Environment.GetEnvironmentVariable("PATH")}");
        Console.WriteLine($"[PymcuCompiler] VIRTUAL_ENV : {Environment.GetEnvironmentVariable("VIRTUAL_ENV")}");

        if (!Directory.Exists(exampleDir))
            throw new DirectoryNotFoundException(
                $"Example directory not found: {exampleDir}");

        var venvBin = Path.Combine(RepoRoot, ".venv", "bin");
        var venvPython = Path.Combine(venvBin, "python3");

        var psi = new ProcessStartInfo
        {
            FileName = venvPython,
            Arguments = $"{PymcuExe} build",
            WorkingDirectory = exampleDir,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            UseShellExecute = false,
        };
        psi.Environment["PYMCU_VERBOSE"] = "1";
        psi.Environment["PATH"] = venvBin + Path.PathSeparator + psi.Environment["PATH"];

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start pymcu process.");

        var stdoutTask = Task.Run(() => proc.StandardOutput.ReadToEnd());
        var stderrTask = Task.Run(() => proc.StandardError.ReadToEnd());

        var finished = proc.WaitForExit(60_000);
        var stdout   = stdoutTask.GetAwaiter().GetResult();
        var stderr   = stderrTask.GetAwaiter().GetResult();

        Console.WriteLine($"[PymcuCompiler] Exit: {(finished ? proc.ExitCode.ToString() : "TIMEOUT")}");
        if (!string.IsNullOrWhiteSpace(stdout)) Console.WriteLine($"stdout:\n{stdout}");
        if (!string.IsNullOrWhiteSpace(stderr)) Console.WriteLine($"stderr:\n{stderr}");

        if (!finished)
        {
            proc.Kill();
            throw new TimeoutException(
                $"pymcu build timed out after 60 s for '{name}'.\n{stdout}\n{stderr}");
        }
        if (proc.ExitCode != 0)
            throw new InvalidOperationException(
                $"pymcu build failed for '{name}' (exit {proc.ExitCode}):\n{stdout}\n{stderr}");

        var hexFile = Path.Combine(exampleDir, "dist", "firmware.hex");
        if (!File.Exists(hexFile))
            throw new FileNotFoundException(
                $"Firmware HEX not found after build: {hexFile}");

        return File.ReadAllText(hexFile);
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "hatch_build.py")) &&
                Directory.Exists(Path.Combine(dir, "examples")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName;
        }
        throw new DirectoryNotFoundException(
            "Cannot locate pymcu-avr repo root (no hatch_build.py + examples/ found in any parent).");
    }
}
