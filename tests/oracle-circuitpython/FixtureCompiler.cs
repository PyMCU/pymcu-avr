using System.Diagnostics;

namespace PyMCU.OracleCircuitPython;

/// <summary>
/// Compiles a tests/integration fixture through the <c>pymcu build</c> CLI and
/// returns the Intel HEX image. A minimal cousin of the integration suite's
/// PymcuCompiler: the fixture is copied to a per-run scratch first, because
/// <c>pymcu build</c> writes into <c>&lt;project&gt;/dist</c> and building in place
/// would race any integration-suite run over the same worktree.
/// </summary>
internal static class FixtureCompiler
{
    private static readonly string ScratchRoot = Path.Combine(
        Path.GetTempPath(),
        "pymcu-oracle-cp-" + Environment.ProcessId + "-"
            + Process.GetCurrentProcess().StartTime.Ticks.ToString("x"));

    private static readonly string PymcuExe =
        Path.Combine(Repo.Root, ".venv", "bin", "pymcu");

    /// <summary>Builds <c>tests/integration/fixtures/{name}</c>; returns the HEX text.</summary>
    public static string BuildFixture(string name)
    {
        var projectDir = Repo.FixtureDir(name);
        if (!Directory.Exists(projectDir))
            throw new DirectoryNotFoundException($"Fixture not found: {projectDir}");

        var scratch = Path.Combine(ScratchRoot, "fx-" + name);
        if (Directory.Exists(scratch)) Directory.Delete(scratch, recursive: true);
        CopyProject(new DirectoryInfo(projectDir), new DirectoryInfo(scratch));

        var venvBin = Path.Combine(Repo.Root, ".venv", "bin");
        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(venvBin, "python3"),
            Arguments = $"{PymcuExe} build",
            WorkingDirectory = scratch,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        psi.Environment["PATH"] = venvBin + Path.PathSeparator + psi.Environment["PATH"];
        var pymcuBin = Environment.GetEnvironmentVariable("PYMCU_BIN");
        if (!string.IsNullOrEmpty(pymcuBin))
            psi.Arguments = $"{pymcuBin} build";

        using var proc = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start pymcu process.");
        var stdoutTask = Task.Run(() => proc.StandardOutput.ReadToEnd());
        var stderrTask = Task.Run(() => proc.StandardError.ReadToEnd());
        if (!proc.WaitForExit(180_000))
        {
            proc.Kill(entireProcessTree: true);
            throw new TimeoutException($"pymcu build timed out after 180 s for '{name}'.");
        }
        var stdout = stdoutTask.GetAwaiter().GetResult();
        var stderr = stderrTask.GetAwaiter().GetResult();
        if (proc.ExitCode != 0)
            throw new InvalidOperationException(
                $"pymcu build failed for '{name}' (exit {proc.ExitCode}):\n{stdout}\n{stderr}");

        var hexFile = Path.Combine(scratch, "dist", "firmware.hex");
        if (!File.Exists(hexFile))
            throw new FileNotFoundException($"Firmware HEX not found after build: {hexFile}");
        return File.ReadAllText(hexFile);
    }

    // dist/ holds stale artifacts and __pycache__ host bytecode; neither is an input.
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
}
