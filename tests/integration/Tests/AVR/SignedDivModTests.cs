using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Exhaustive validation of signed integer floor division/modulo (codegen backlog A38).
/// For each width the generator emits every sign combination of (a, b) plus exact/inexact,
/// zero-dividend, ±1, and min/max edge cases, runs them through the __divs*/__mods* runtime
/// routines on the simulator, and compares against a Python floor-semantics oracle. A wrong
/// sign, a missing floor correction, or a clobbered register shows up as a mismatch.
/// </summary>
[TestFixture]
public class SignedDivModTests
{
    [TestCase("int8", 1)]
    [TestCase("int16", 2)]
    [TestCase("int32", 4)]
    public void FloorDivMod_MatchesPythonSemantics(string typeName, int bytes)
    {
        var prog = SignedDivModProgram.Generate(typeName, bytes);
        var hex = PymcuCompiler.BuildSource(prog.Source);

        var uno = new ArduinoUnoSimulation();
        uno.WithHex(hex);
        uno.RunUntilSerial(uno.Serial, "GO\n", maxMs: 500);
        uno.Serial.InjectByte(prog.InputByte);
        uno.RunUntilSerial(uno.Serial, s => CountNewlines(s) >= prog.Expected.Count + 1, maxMs: 12000);

        var got = ParseSignedAfterBanner(uno.Serial.Text, prog.Expected.Count);
        got.Should().Equal(prog.Expected,
            $"{typeName}: simulated signed //,% must match Python's floor semantics.\n" +
            $"--- serial ---\n{uno.Serial.Text}");
    }

    private static int CountNewlines(string s)
    {
        int n = 0;
        foreach (var c in s) if (c == '\n') n++;
        return n;
    }

    private static List<long> ParseSignedAfterBanner(string text, int count)
    {
        var lines = text.Replace("\r", "").Split('\n');
        int start = Array.FindIndex(lines, l => l.Trim() == "GO");
        var result = new List<long>();
        for (int i = start + 1; i < lines.Length && result.Count < count; i++)
        {
            var t = lines[i].Trim();
            if (t.Length == 0) continue;
            if (long.TryParse(t, out long v)) result.Add(v);
            else break;
        }
        return result;
    }
}
