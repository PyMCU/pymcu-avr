// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration test for ptr(BASE + offset) compile-time address arithmetic.
/// The fixture writes PORTB through ptr(PINB + 2): PINB=0x23, +2 = PORTB=0x25.
/// If the offset is applied to the register's ADDRESS (correct) the pins go high;
/// dereferencing PINB instead would compute a garbage address and miss PORTB.
/// </summary>
[TestFixture]
public class PtrOffsetTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("ptr-offset"));

    [Test]
    public void PtrPlusOffset_LandsOnPortB_DrivesPinsHigh()
    {
        var uno = _session.Reset();
        uno.RunMilliseconds(5);
        uno.PortB.Should().HavePinHigh(5);
        uno.PortB.Should().HavePinHigh(0);
    }
}
