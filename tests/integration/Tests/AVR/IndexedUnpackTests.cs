// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/indexed-unpack: <c>word[i*2], crc[i*2], ... = struct.unpack(...)</c>
/// stores each field. Adafruit sht31d writes that assignment in <c>_unpack</c>.
///
/// WHAT DISCRIMINATES: prints 18, 171, 86, 205.
/// </summary>
[TestFixture]
public class IndexedUnpackTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("indexed-unpack"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("indexed-unpack"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void SubscriptUnpack_StoresEachStructField()
    {
        FullRun(_session).Serial.Text.Should().Contain("18\n171\n86\n205\nEND\n",
            because: "word[i*2], crc[i*2], ... = unpack(\">HBHB\", data[i*6:i*6+6]) is 0x1234, 0xAB, 0x5678, 0xCD");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("18\n171\n86\n205\nEND\n",
            because: "both front ends must desugar a subscript unpack the same way");
    }
}
