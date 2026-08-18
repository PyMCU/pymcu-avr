// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// uart.write_hex() at several call sites, over values that hit both halves of
/// the digit/letter split and both nibble positions. The @inline expansion adds
/// 48 or 55 depending on the nibble, and each site gets its own copy — a build
/// that shares one site's constants, or one that loses the byte on its way out of
/// an outlined copy, prints the wrong digits here rather than in a driver.
/// </summary>
[TestFixture]
public class UartWriteHexTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("uart-write-hex"));

    [Test]
    public void EveryNibblePrintsItsOwnDigit()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "FF 3C 00 A5 0F\n", maxMs: 3000);
        uno.Serial.Should().ContainLine("FF 3C 00 A5 0F");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
