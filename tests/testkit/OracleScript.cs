using System.Diagnostics;

namespace PyMCU.TestKit;

/// <summary>
/// Runs a fixture's <c>oracle/oracle.py</c> -- the vendored library sources
/// executed under stock CPython against a fake I2C bus -- and parses the
/// transaction stream it prints, one line per transaction.
/// </summary>
public static class OracleScript
{
    public static List<I2cTransaction> Run(string oracleScriptPath, string pythonExe)
    {
        var psi = new ProcessStartInfo
        {
            FileName = pythonExe,
            Arguments = oracleScriptPath,
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

        return I2cTransaction.ParseStream(stdout);
    }
}
