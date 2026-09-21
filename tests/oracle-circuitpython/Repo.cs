namespace PyMCU.OracleCircuitPython;

/// <summary>
/// Locates the pymcu-avr repo root and the paths the harness needs. Same
/// marker as the integration suite's PymcuCompiler (hatch_build.py at the
/// root), so both projects agree on what "the repo" is.
/// </summary>
internal static class Repo
{
    public static string Root { get; } = FindRoot();

    /// <summary>tests/integration/fixtures/{name} -- the fixture sources are reused, not copied.</summary>
    public static string FixtureDir(string name) =>
        Path.Combine(Root, "tests", "integration", "fixtures", name);

    public static string VenvPython =>
        Path.Combine(Root, ".venv", "bin", "python");

    private static string FindRoot()
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
