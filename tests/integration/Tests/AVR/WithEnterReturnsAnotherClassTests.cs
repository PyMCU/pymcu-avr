// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// PyMCU/PyMCU#454. A with-block's context manager can hand back an instance of a DIFFERENT
/// class than its own -- reduced from adafruit_bus_device's SPIDevice.__enter__, which returns
/// self.spi (a busio.SPI), not the SPIDevice itself; adafruit_mcp3xxx's own `with
/// self._spi_device as spi: spi.write_readinto(...)` is exactly this shape. Covers both a
/// module-level with-block and one inside a method, across two files.
/// </summary>
[TestFixture]
public class WithEnterReturnsAnotherClassTests
{
    /// <summary>
    /// 11 (module-level with) and 13 (with inside a method) are the constructor arguments
    /// each Manager was built with. The earlier bug mangled the bound name to an undefined
    /// free function, so this program did not build at all; a wrong-but-building reading (the
    /// with-header's own discarded temporary) would not print these specific values.
    /// </summary>
    [Test]
    public void Issue454_TheBoundNameCallsAMethodOnTheReturnedFieldsClass()
    {
        var uno = new SimSession(
            PymcuCompiler.BuildFixture("with-enter-returns-another-class")).Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 400);

        uno.Serial.Text.Should().Contain("WE\n11\n13\nEND\n");
    }
}
