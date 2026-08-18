// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// microcontroller.nvm[0:4] = b'...' unrolls into per-byte __setitem__ EEPROM
/// writes, and print(nvm[0:4]) / print(bytearray) stream the CPython
/// bytearray(b'...') repr (printable ASCII as itself, \t \n \r and \xNN
/// escapes). print(bytearray) previously streamed the array variable through
/// the u8 decimal formatter and printed garbage.
///
/// Own simulation per test (not SimSession): the fixture writes EEPROM and the
/// AVR8Sharp EEPROM peripheral keeps write-timing counters a shared session
/// reset does not clear (see CompatCpMicrocontrollerTests).
/// </summary>
[TestFixture]
public class NvmSliceReprTests
{
    private string _hex = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _hex = PymcuCompiler.BuildFixture("nvm-slice-repr");

    [Test]
    public void SliceWrite_ThenSliceRead_PrintsCPythonRepr()
    {
        var uno = new ArduinoUnoSimulation();
        uno.WithHex(_hex);
        uno.RunUntilSerial(uno.Serial, "D", maxMs: 4000);
        uno.Serial.Text.Should().Be(
            "bytearray(b'\\xcc\\x10\\xca\\xfe')\n" +
            "bytearray(b'\\x01A\\n')\n" +
            "D");
    }
}
