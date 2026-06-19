using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/zca-outline-selfcall -- RFC 0001 phase 2:
/// an outlined ZCA method that calls a SIBLING method on self.
///
/// Dev.compute() calls self.helper() twice. It used to be force-inlined per call
/// site (it touches self via a method call, not just a field); now it is outlined
/// once and forwards its own self to the shared Dev_helper body.
///   a = Dev(3): compute(1) = helper(1)+helper(2) = 4+5 = 9
///   b = Dev(5): compute(2) = helper(2)+helper(3) = 7+8 = 15
/// Seeing bytes 9 and 15 proves compute forwards each instance's runtime base into
/// the shared helper. The asm assertion proves there is exactly one shared body for
/// each method (no per-call-site / per-instance duplication).
/// </summary>
[TestFixture]
public class ZcaOutlineSelfCallTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("zca-outline-selfcall"));

    [Test]
    public void OutlinedMethod_CallingSibling_ForwardsSelfCorrectly()
    {
        var uno = _session.Reset();
        uno.RunUntilSerialBytes(uno.Serial, 5, maxMs: 400); // "SC\n" (3) + 9 + 15

        var bytes = uno.Serial.Bytes;
        bytes.Length.Should().BeGreaterThanOrEqualTo(5, "banner 'SC\\n' + two result bytes");
        bytes[^2].Should().Be(9,  "Dev(3).compute(1) = (3+1)+(3+2) = 9");
        bytes[^1].Should().Be(15, "Dev(5).compute(2) = (5+2)+(5+3) = 15");
    }

    [Test]
    public void BothMethods_AreSharedSubroutines_NotDuplicated()
    {
        var asm = File.ReadAllText(Path.Combine(
            PymcuCompiler.FixtureDir("zca-outline-selfcall"), "dist", "debug", "firmware.asm"));
        CountLabel(asm, "Dev_compute").Should().Be(1, "compute is outlined once, not inlined per call site");
        CountLabel(asm, "Dev_helper").Should().Be(1, "helper is outlined once and shared by compute");
    }

    private static int CountLabel(string asm, string label) =>
        asm.Split('\n').Count(l => l.TrimStart().StartsWith(label + ":"));
}
