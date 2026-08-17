using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

[TestFixture]
public class WideCompareTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("wide-compare"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("END\n"), maxMs: 2000);
        return uno;
    }

    private void Expect(string label, string bits, string because)
        => Boot().Serial.Text.Should().Contain($"{label}:{bits}", because);

    [Test]
    public void Boot_SendsBanner() =>
        Boot().Serial.Text.Should().Contain("WIDE");

    [Test]
    public void Int16_HighByteDecidesOrder() =>
        Expect("A", "001101",
            "256 vs 3 as int16: the low bytes (0x00 vs 0x03) invert the true ordering");

    [Test]
    public void Int16_HighByteDecidesOrder_Reversed() =>
        Expect("B", "110001",
            "3 vs 256 as int16 must mirror case A");

    [Test]
    public void Int16_EqualityLooksAtEveryByte() =>
        Expect("C", "01",
            "256 == 0 as int16 is false even though both low bytes are 0x00");

    [Test]
    public void Uint16_AboveSignedRangeOrdersUnsigned() =>
        Expect("D", "001101",
            "0x8000 vs 5 as uint16 must use unsigned branches over the full width");

    [Test]
    public void Uint16_EqualityLooksAtEveryByte() =>
        Expect("E", "01",
            "0x8000 == 0 as uint16 is false");

    [Test]
    public void Int32_HighBytesDecideOrder() =>
        Expect("F", "001101",
            "65536 vs 3 as int32: the low three bytes are all zero");

    [Test]
    public void Int32_HighBytesDecideOrder_Reversed() =>
        Expect("G", "110001",
            "3 vs 65536 as int32 must mirror case F");

    [Test]
    public void Int32_EqualityLooksAtEveryByte() =>
        Expect("H", "01",
            "65536 == 0 as int32 is false even though the low byte matches");

    [Test]
    public void Uint32_AboveSignedRangeOrdersUnsigned() =>
        Expect("I", "001101",
            "0x80000000 vs 5 as uint32 must stay unsigned across all four bytes");

    [Test]
    public void Uint32_EqualityLooksAtEveryByte() =>
        Expect("J", "01",
            "0x80000000 == 0 as uint32 is false");

    [Test]
    public void Int8_StaysSingleByte() =>
        Expect("K", "001101",
            "100 vs 3 as int8 must keep working with a bare CP and no CPC behind it");

    [Test]
    public void NegativeInt16_KeepsSignedBranches() =>
        Expect("L", "110001",
            "-300 vs 5 as int16 must still pick the signed branch after the widening");
}
