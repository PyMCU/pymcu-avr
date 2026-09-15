using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/bytes-param (PyMCU#365): a parameter annotated `bytes`.
///
/// No part of the compiler had a width for the name, so the annotation was taken for a CLASS
/// and the function was registered for call-site expansion instead of compiled as a
/// subroutine. `bytearray` was unaffected, which is what made it easy to walk past: the same
/// program with one word changed behaved.
///
/// The two halves of the loss are measured separately here, because they are not both visible
/// in the same place.
///
/// The SYMBOLS are the part that was lost. Against the baseline compiler this fixture builds
/// green with `first`, `third` and `total` absent from `firmware.asm` entirely. That is what
/// reached a CircuitPython native module as `undefined symbol` out of `tools/mpy_ld.py`, which
/// reads as a linker problem and is not one.
///
/// The VALUES came out right on the baseline and are pinned anyway, because the fix moves each
/// of those bodies out of an expansion and into a subroutine with an argument ABI, and a
/// pointer that arrives in the wrong register answers a plausible wrong number rather than
/// failing. gbuf is 5, 6, 7, and the numbers are CPython's for the same bytes.
/// </summary>
[TestFixture]
public class BytesParamTests
{
    private static SimSession _session = null!;
    private static string[] _lines = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session = new SimSession(PymcuCompiler.BuildFixture("bytes-param"));
        var asm = File.ReadAllText(Path.Combine(
            PymcuCompiler.FixtureDir("bytes-param"), "dist", "debug", "firmware.asm"));
        _lines = asm.Split('\n').Select(l => l.Trim()).ToArray();
    }

    private static string Output()
    {
        var uno = _session.Reset();
        uno.RunMilliseconds(400);
        return uno.Serial.Text;
    }

    // ── the symbols: what a `bytes` parameter used to cost ──────────────────────────────

    [TestCase("first")]
    [TestCase("third")]
    [TestCase("total")]
    public void AFunctionWithABytesParameterIsInTheFirmware(string name)
        => _lines.Should().Contain(name + ":",
            "a `bytes` parameter used to register the function for call-site expansion, so it "
            + "was compiled nowhere and the build said so about nothing");

    [Test]
    public void TheBytearrayControlIsThereToo()
        => _lines.Should().Contain("first_control:",
            "the control that behaved all along, one word apart from the first one");

    // ── the values, over the argument ABI the fix puts them on ──────────────────────────

    [Test]
    public void AConstantIndexOnABytesParameterReadsTheFirstByte()
        => Output().Should().Contain("first 5");

    [Test]
    public void AConstantIndexReadsTheByteItNames()
        => Output().Should().Contain("third 7", "buf[2] is the third byte, not a bit of anything");

    [Test]
    public void ARunTimeIndexOnABytesParameterWalksTheBuffer()
        => Output().Should().Contain("total 18",
            "5 + 6 + 7 -- the pointer has to survive the call, not be folded at one site");

    [Test]
    public void ABytearrayParameterAnswersTheSame()
        => Output().Should().Contain("ctl 5");

    [Test]
    public void TheProgramRunsToItsEnd() => Output().Should().Contain("done");
}
