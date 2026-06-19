using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Property/differential validation of signed arithmetic fidelity: each seed generates a
/// register-pressure program over int8/int16 locals using the whole signed operator set
/// (+ - * &amp; | ^, floor // and %, arithmetic &gt;&gt; and &lt;&lt;, and a call-spanning signed
/// helper) and compares the simulated output against a C# fixed-width signed oracle. This is the
/// systematic check that PyMCU's signed semantics match Python (within fixed-width wrapping).
/// </summary>
[TestFixture]
public class SignedStressTests
{
    private static readonly int[] Seeds = { 1, 2, 3, 7, 13, 42, 99, 123, 200, 255, 777, 2024 };

    [TestCaseSource(nameof(Seeds))]
    public void GeneratedProgram_MatchesSignedReferenceSemantics(int seed)
    {
        var prog = SignedStressProgram.Generate(seed);
        var hex = PymcuCompiler.BuildSource(prog.Source);

        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(prog.InputByte);
        uno.RunUntilSerial(uno.Serial, s => CountNewlines(s) >= prog.Expected.Count + 1, maxMs: 6000);

        var got = ParseSignedAfterBanner(uno.Serial.Text, prog.Expected.Count);
        got.Should().Equal(prog.Expected,
            $"seed {seed}: simulated signed output must match the fixed-width signed reference.\n" +
            $"--- program ---\n{prog.Source}\n--- serial ---\n{uno.Serial.Text}");
    }

    // Heavy sweep, [Explicit] like the other stress sweeps.
    [Test, Explicit("heavy: run on demand to validate a signed-codegen change")]
    public void Sweep()
    {
        var failures = new List<string>();
        for (int seed = 1; seed <= 100; seed++)
        {
            var prog = SignedStressProgram.Generate(seed);
            var hex = PymcuCompiler.BuildSource(prog.Source);
            var uno = new ArduinoUnoSimulation();
            uno.WithHex(hex);
            uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
            uno.Serial.InjectByte(prog.InputByte);
            uno.RunUntilSerial(uno.Serial, s => CountNewlines(s) >= prog.Expected.Count + 1, maxMs: 6000);
            var got = ParseSignedAfterBanner(uno.Serial.Text, prog.Expected.Count);
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

    private static List<int> ParseSignedAfterBanner(string text, int count)
    {
        var lines = text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        var result = new List<int>();
        for (int i = start + 1; i < lines.Length && result.Count < count; i++)
        {
            var t = lines[i].Trim();
            if (t.Length == 0) continue;
            if (int.TryParse(t, out int v)) result.Add(v);
            else break;
        }
        return result;
    }
}
