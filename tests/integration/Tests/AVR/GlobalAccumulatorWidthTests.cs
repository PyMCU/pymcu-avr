using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/global-accumulator-width (PyMCU#205).
///
/// A module-level name typed uint8 from its initializer stayed uint8 when a function assigned
/// it a uint16, so the store truncated. Silent: the program built, ran, and reported small
/// plausible numbers, which on a sensor average is indistinguishable from noise or a wiring
/// fault.
///
/// Every assertion reads the HIGH byte. The low byte cannot discriminate -- 307 truncated to
/// eight bits keeps its low byte unchanged -- so a test written against the value as printed
/// would pass on the broken compiler for the wrong reason.
/// </summary>
[TestFixture]
public class GlobalAccumulatorWidthTests
{
    private const int GPIOR0_ADDR = 0x3E;
    private const int GPIOR1_ADDR = 0x4A;
    private const int GPIOR2_ADDR = 0x4B;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
        => _session = new SimSession(PymcuCompiler.BuildFixture("global-accumulator-width"));

    private static ArduinoUnoSimulation RunWithSeed(byte seed)
    {
        var uno = _session.Reset();
        uno.Data[GPIOR0_ADDR] = seed;
        uno.RunToBreak();
        return uno;
    }

    // total = 0 at module level, then total = total + r with r uint16 inside a function.
    [TestCase((byte)7, (byte)1)]    // 300 + 7   = 307 = 0x0133
    [TestCase((byte)220, (byte)2)]  // 300 + 220 = 520 = 0x0208
    public void AnAccumulatorFedAWiderValueKeepsItsHighByte(byte seed, byte expectedHigh)
        => RunWithSeed(seed).Data[GPIOR1_ADDR].Should().Be(expectedHigh,
            "the accumulator must widen to the value it is assigned, not truncate to its initializer");

    // The bound the issue recorded as already working. It works at module scope; from inside a
    // function a literal reassignment truncated exactly like the accumulator.
    [TestCase((byte)7)]
    [TestCase((byte)220)]
    public void ALiteralReassignedFromInsideAFunctionKeepsItsHighByte(byte seed)
        => RunWithSeed(seed).Data[GPIOR2_ADDR].Should().Be(1,
            "counter = 400 inside a function must widen the global, as it does at module scope");

    // The failure mode: the high byte lost entirely, which is what truncation looks like.
    [Test]
    public void NeitherGlobalReportsATruncatedHighByte()
    {
        var uno = RunWithSeed(7);

        uno.Data[GPIOR1_ADDR].Should().NotBe(0, "a zero high byte is the eight-bit store");
        uno.Data[GPIOR2_ADDR].Should().NotBe(0, "a zero high byte is the eight-bit store");
    }
}
