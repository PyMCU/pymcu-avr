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

    /// <summary>
    /// Absolute path of a fixture directory — for tests that inspect build
    /// artifacts (e.g. <c>dist/debug/firmware.asm</c>) after <see cref="BuildFixture"/>.
    /// </summary>
    public static string FixtureDir(string name)
        => Path.Combine(RepoRoot, "tests", "integration", "fixtures", name);

    /// <summary>
    /// Compiles the fixture at <c>tests/integration/fixtures/{name}</c> with the IR
    /// optimizer disabled (<c>PYMCU_NO_OPT=1</c>).
    /// </summary>
    /// <remarks>
    /// The project is copied to a scratch directory first: <c>pymcu build</c> always
    /// writes to <c>&lt;project&gt;/dist</c>, so building in place would overwrite the
    /// optimized artifacts other fixtures read (e.g. <c>dist/debug/firmware.asm</c>).
    /// </remarks>
    public static string BuildFixtureUnoptimized(string name)
        => Cache.GetOrAdd("fx-noopt:" + name,
            _ => new Lazy<string>(() => CompileUnoptimized(
                Path.Combine(RepoRoot, "tests", "integration", "fixtures", name), "fx", name))).Value;

    /// <summary>
    /// Compiles the showcase example at <c>examples/{name}</c> with the IR optimizer
    /// disabled (<c>PYMCU_NO_OPT=1</c>). See <see cref="BuildFixtureUnoptimized"/>.
    /// </summary>
    public static string BuildUnoptimized(string name)
        => Cache.GetOrAdd("ex-noopt:" + name,
            _ => new Lazy<string>(() => CompileUnoptimized(
                Path.Combine(RepoRoot, "examples", name), "ex", name))).Value;

    /// <summary>
    /// Absolute path of an example directory — the differential corpus enumerates
    /// both example and fixture projects to read their target chip.
    /// </summary>
    public static string ExampleDir(string name)
        => Path.Combine(RepoRoot, "examples", name);

    /// <summary>Absolute path of the repository root.</summary>
    public static string Root => RepoRoot;

    /// <summary>
    /// Compiles an arbitrary generated <c>main.py</c> source (e.g. a property/differential
    /// test program for the register allocator). The program is materialized into a
    /// throwaway project under the system temp directory and built with <c>pymcu build</c>.
    /// Cached by content hash so identical programs compile once.
    /// </summary>
    public static string BuildSource(string mainPy)
        => Cache.GetOrAdd("src:" + Sha(mainPy), _ => new Lazy<string>(() => CompileSource(mainPy))).Value;

    private static string Sha(string s)
    {
        var bytes = System.Security.Cryptography.SHA1.HashData(System.Text.Encoding.UTF8.GetBytes(s));
        return Convert.ToHexString(bytes);
    }

    private static string CompileSource(string mainPy)
    {
        var dir = Path.Combine(Path.GetTempPath(), "pymcu-gen", Sha(mainPy)[..16]);
        Directory.CreateDirectory(Path.Combine(dir, "src"));
        File.WriteAllText(Path.Combine(dir, "pyproject.toml"),
            "[project]\n" +
            "name = \"gen\"\n" +
            "version = \"0.1.0\"\n" +
            "requires-python = \">=3.11\"\n" +
            "dependencies = [\"pymcu-stdlib>=0.1.2a5\", \"pymcu>=0.1.0a27\"]\n\n" +
            "[tool.pymcu]\n" +
            "target = \"atmega328p\"\n" +
            "frequency = 16000000\n" +
            "sources = \"src\"\n" +
            "entry = \"main.py\"\n");
        File.WriteAllText(Path.Combine(dir, "src", "main.py"), mainPy);
        return Compile(dir, "gen-" + Sha(mainPy)[..8]);
    }

    // ── Internal ─────────────────────────────────────────────────────────────

    private static string Compile(string exampleDir, string name,
        IReadOnlyDictionary<string, string>? extraEnv = null)
    {
        BuildGate.Wait();
        try { return CompileImpl(exampleDir, name, extraEnv); }
        finally { BuildGate.Release(); }
    }

    private static readonly IReadOnlyDictionary<string, string> NoOptEnv =
        new Dictionary<string, string> { ["PYMCU_NO_OPT"] = "1" };

    private static string CompileUnoptimized(string projectDir, string kind, string name)
    {
        if (!Directory.Exists(projectDir))
            throw new DirectoryNotFoundException($"Project directory not found: {projectDir}");

        var scratch = Path.Combine(Path.GetTempPath(), "pymcu-noopt", kind + "-" + name);
        if (Directory.Exists(scratch)) Directory.Delete(scratch, recursive: true);
        CopyProject(new DirectoryInfo(projectDir), new DirectoryInfo(scratch));

        return Compile(scratch, name + " (no-opt)", NoOptEnv);
    }

    // dist/ holds the previous build's artifacts and __pycache__ holds host bytecode;
    // neither is an input, and copying dist/ would let a stale firmware.hex survive a
    // failed build and be read as if it were fresh.
    private static void CopyProject(DirectoryInfo src, DirectoryInfo dst)
    {
        dst.Create();
        foreach (var file in src.EnumerateFiles())
            file.CopyTo(Path.Combine(dst.FullName, file.Name), overwrite: true);
        foreach (var dir in src.EnumerateDirectories())
        {
            if (dir.Name is "dist" or "__pycache__" or ".venv") continue;
            CopyProject(dir, new DirectoryInfo(Path.Combine(dst.FullName, dir.Name)));
        }
    }

    // Verbose when the test runner itself is in debug mode.
    // RUNNER_DEBUG=1 is set automatically by GitHub Actions when
    // "Enable debug logging" is enabled in repository settings.
    private static readonly bool Verbose =
        Environment.GetEnvironmentVariable("PYMCU_VERBOSE") == "1" ||
        Environment.GetEnvironmentVariable("RUNNER_DEBUG")  == "1";

    private static string CompileImpl(string exampleDir, string name,
        IReadOnlyDictionary<string, string>? extraEnv = null)
    {
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
        if (Verbose)
            psi.Environment["PYMCU_VERBOSE"] = "1";
        psi.Environment["PATH"] = venvBin + Path.PathSeparator + psi.Environment["PATH"];
        if (extraEnv != null)
            foreach (var (key, value) in extraEnv)
                psi.Environment[key] = value;

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start pymcu process.");

        var stdoutTask = Task.Run(() => proc.StandardOutput.ReadToEnd());
        var stderrTask = Task.Run(() => proc.StandardError.ReadToEnd());

        var finished = proc.WaitForExit(60_000);
        var stdout   = stdoutTask.GetAwaiter().GetResult();
        var stderr   = stderrTask.GetAwaiter().GetResult();

        var failed = !finished || proc.ExitCode != 0;
        if (failed || Verbose)
        {
            if (failed)
            {
                Console.WriteLine($"[PymcuCompiler] Build failed: {name}");
                Console.WriteLine($"[PymcuCompiler] RepoRoot    : {RepoRoot}");
                Console.WriteLine($"[PymcuCompiler] ExampleDir  : {exampleDir}");
                Console.WriteLine($"[PymcuCompiler] PATH        : {psi.Environment["PATH"]}");
                Console.WriteLine($"[PymcuCompiler] VIRTUAL_ENV : {Environment.GetEnvironmentVariable("VIRTUAL_ENV")}");
            }
            Console.WriteLine($"[PymcuCompiler] Exit: {(finished ? proc.ExitCode.ToString() : "TIMEOUT")}");
            if (!string.IsNullOrWhiteSpace(stdout)) Console.WriteLine($"stdout:\n{stdout}");
            if (!string.IsNullOrWhiteSpace(stderr)) Console.WriteLine($"stderr:\n{stderr}");
        }

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
