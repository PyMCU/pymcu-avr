using FluentAssertions;
using NUnit.Framework;
using System.Text.RegularExpressions;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/mega-ramstart (pymcu-avr: RAMSTART rescue).
///
/// The fixture targets the ATmega2560, whose SRAM begins at 0x0200 -- everything
/// below is register and extended-I/O space. The backend used to emit
/// `0x0100 + offset` for every absolute slot/array access past the Y+63
/// displacement window, which is only right on parts whose RAMSTART is 0x0100.
/// On this chip each of those accesses lands 0x100 low: a store is lost to a
/// peripheral register and the matching load answers whatever the register holds.
///
/// Avr8Sharp cannot execute ATmega2560 firmware (its RCALL keeps a phantom
/// 0x10000 in the PC, so no program survives its first call), so this test
/// asserts on the emitted listing rather than simulated behaviour:
///   - the fixture does reach the absolute path (some access at 0x0200+), and
///   - no absolute access lands below RAMSTART.
/// </summary>
[TestFixture]
public class MegaRamstartTests
{
    private static string _asm = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        PymcuCompiler.BuildFixture("mega-ramstart");
        _asm = File.ReadAllText(
            Path.Combine(PymcuCompiler.FixtureDir("mega-ramstart"), "dist", "firmware.gas.asm"));
    }

    private static List<int> AbsoluteAddresses()
        => Regex.Matches(_asm, @"\b(?:LDS|STS)\b[^\n;]*")
            .SelectMany(m => Regex.Matches(m.Value, @"0x([0-9A-Fa-f]{4})"))
            .Select(m => Convert.ToInt32(m.Groups[1].Value, 16))
            .ToList();

    [Test]
    public void FixtureReachesTheAbsolutePath()
    {
        // Without an access at or above RAMSTART the second assertion below is
        // vacuous: this proves the fixture really exercises LDS/STS on slots and
        // array elements past the Y+63 window.
        AbsoluteAddresses().Should().Contain(a => a >= 0x0200);
    }

    [Test]
    public void NoAbsoluteAccessLandsBelowRamstart()
    {
        // The buggy backend emits 0x0100-based addresses here. Legitimate
        // sub-0x0200 operands exist only for extended-I/O registers (0x60-0xFF),
        // so the 0x0100-0x01FF band must stay empty.
        AbsoluteAddresses().Should().NotContain(a => a >= 0x0100 && a < 0x0200);
    }
}
