using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/volatile-flag — ISR-shared plain globals.
///
/// Two plain uint8 module globals are shared between the INT0 ISR and main with
/// no GPIOR idiom and no manual volatile handling: <c>flag</c> (ISR sets, main
/// polls and clears) and <c>presses</c> (ISR increments, main reads). The core
/// compiler must detect both as ISR-shared (volatile semantics: no constant
/// caching across the store/poll sequence) and the AVR backend must promote
/// them to GPIOR0 (0x3E) and GPIOR1 (0x4A) — verified both behaviorally and by
/// peeking the I/O registers in the simulator.
/// </summary>
[TestFixture]
public class VolatileFlagTests
{
    private const int Gpior0 = 0x3E; // 'flag'    — most-used global, bit-addressable GPIOR
    private const int Gpior1 = 0x4A; // 'presses'

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("volatile-flag"));

    [Test]
    public void Boot_SendsBanner()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "VOLATILE");
        uno.Serial.Should().ContainLine("VOLATILE");
    }

    [Test]
    public void PlainGlobalFlag_IsrToMain_DeliversEachPress()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "VOLATILE\n");
        var before = uno.Serial.ByteCount;

        for (var i = 0; i < 3; i++)
        {
            Press(uno);
            uno.RunUntilSerialBytes(uno.Serial, before + (i + 1) * 2, maxMs: 200);
        }

        // presses byte + '\n' per press: 1, 2, 3 — the ISR increments through the
        // shared global and main observes every change (volatile poll loop works).
        uno.Serial.Bytes[before].Should().Be(0x01, "first press → presses = 1");
        uno.Serial.Bytes[before + 2].Should().Be(0x02, "second press → presses = 2");
        uno.Serial.Bytes[before + 4].Should().Be(0x03, "third press → presses = 3");
    }

    [Test]
    public void IsrSharedGlobals_LiveInGpior_NotSram()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "VOLATILE\n");
        var before = uno.Serial.ByteCount;

        Press(uno);
        uno.RunUntilSerialBytes(uno.Serial, before + 2, maxMs: 200);
        Press(uno);
        uno.RunUntilSerialBytes(uno.Serial, before + 4, maxMs: 200);

        // 'presses' was promoted to GPIOR1: the lifetime counter must be readable
        // straight out of the I/O register.
        uno.Data[Gpior1].Should().Be(2, "'presses' lives in GPIOR1 (0x4A) after promotion");
        // 'flag' was promoted to GPIOR0 and main already consumed the press.
        uno.Data[Gpior0].Should().Be(0, "'flag' lives in GPIOR0 (0x3E) and is cleared after the poll");
    }

    [Test]
    public void Backend_EmitsGpiorPromotionMarkers()
    {
        var asmPath = Path.Combine(
            PymcuCompiler.FixtureDir("volatile-flag"), "dist", "debug", "firmware.asm");
        File.Exists(asmPath).Should().BeTrue($"debug asm should exist at {asmPath}");

        var asm = File.ReadAllText(asmPath);
        asm.Should().Contain("volatile 'flag' -> GPIOR @ 0x3E",
            "the most-used ISR-shared global gets the bit-addressable GPIOR0");
        asm.Should().Contain("volatile 'presses' -> GPIOR @ 0x4A");
    }

    private ArduinoUnoSimulation Sim()
    {
        var uno = _session.Reset();
        uno.PortD.SetPinValue(2, true); // button released (INT0 fires on falling edge)
        return uno;
    }

    private static void Press(ArduinoUnoSimulation uno)
    {
        uno.PortD.SetPinValue(2, true);
        uno.RunMilliseconds(1);
        uno.PortD.SetPinValue(2, false);
    }
}
