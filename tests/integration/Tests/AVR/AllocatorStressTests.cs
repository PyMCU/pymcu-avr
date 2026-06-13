using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Property / differential tests for the AVR register allocator and codegen.
///
/// Each seed (see <see cref="AllocStressProgram"/>) generates a register-pressure-heavy
/// program whose final variable values are also computed in C# with PyMCU's exact
/// fixed-width semantics. The program is compiled, simulated, and its printed decimals are
/// compared against that oracle. A clobbered/mis-homed register, a dropped call-spanning
/// value, or any allocation bug shows up as a mismatch — the safety net for the planned
/// register-allocator redesign (codegen backlog A17/A31): run this before and after, the
/// values must stay identical.
/// </summary>
[TestFixture]
public class AllocatorStressTests
{
    // A spread of fixed seeds: deterministic and reproducible. Each is a distinct program;
    // builds are content-hash cached. Keep the count modest — each compiles + simulates.
    private static readonly int[] Seeds = { 1, 2, 3, 7, 13, 42, 99, 123, 777, 2024 };

    [TestCaseSource(nameof(Seeds))]
    public void GeneratedProgram_MatchesReferenceSemantics(int seed)
    {
        var prog = AllocStressProgram.Generate(seed);
        var hex = PymcuCompiler.BuildSource(prog.Source);

        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);

        // Run to the "GO" banner, then inject the runtime seed byte the program reads with
        // read_blocking(). Every value derives from it, so nothing constant-folds.
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(prog.InputByte);

        // "GO\n" banner + one decimal per printed value, each "<n>\n".
        int wantLines = prog.Expected.Count + 1;
        uno.RunUntilSerial(uno.Serial, s => CountNewlines(s) >= wantLines, maxMs: 4000);

        var got = ParseDecimalsAfterBanner(uno.Serial.Text, prog.Expected.Count);

        got.Should().Equal(prog.Expected,
            $"seed {seed}: simulated output must match the fixed-width reference.\n" +
            $"--- program ---\n{prog.Source}\n--- serial ---\n{uno.Serial.Text}");
    }

    // Heavy sweep for validating the register-allocator redesign: run before and after the
    // change, the results must be identical. [Explicit] so it does not slow the normal CI run
    // (each case compiles + simulates). Invoke with:
    //   dotnet test --filter "FullyQualifiedName~AllocatorStressTests.Sweep"
    [Test, Explicit("heavy: run on demand to validate an allocator/codegen change")]
    public void Sweep()
    {
        var failures = new List<string>();
        for (int seed = 1; seed <= 100; seed++)
        {
            var prog = AllocStressProgram.Generate(seed);
            var hex = PymcuCompiler.BuildSource(prog.Source);
            var uno = new ArduinoUnoSimulation();
            uno.WithHex(hex);
            uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
            uno.Serial.InjectByte(prog.InputByte);
            uno.RunUntilSerial(uno.Serial, s => CountNewlines(s) >= prog.Expected.Count + 1, maxMs: 4000);
            var got = ParseDecimalsAfterBanner(uno.Serial.Text, prog.Expected.Count);
            if (!got.SequenceEqual(prog.Expected))
                failures.Add($"seed {seed}: expected [{string.Join(",", prog.Expected)}] got [{string.Join(",", got)}]");
        }
        failures.Should().BeEmpty($"{failures.Count} seed(s) miscompiled:\n{string.Join("\n", failures)}");
    }

    private static int CountNewlines(string s)
    {
        int n = 0;
        foreach (var c in s) if (c == '\n') n++;
        return n;
    }

    // Skips the "GO" banner, then reads the next `count` lines as decimals.
    private static List<int> ParseDecimalsAfterBanner(string text, int count)
    {
        var lines = text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        var result = new List<int>();
        for (int i = start + 1; i < lines.Length && result.Count < count; i++)
        {
            var t = lines[i].Trim();
            if (t.Length == 0) continue;
            if (int.TryParse(t, out int v)) result.Add(v);
            else break;   // unexpected garbage — stop; the Equal assertion will report it
        }
        return result;
    }
}
