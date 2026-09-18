// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/repeated-none-list: <c>[None] * N</c> is a fixed SRAM array.
/// Adafruit dps310 writes <c>coeffs = [None] * 18</c> then fills it in
/// <c>range(18)</c>; pca9685 writes <c>self._channels = [None] * len(self)</c>
/// with <c>__len__</c> after <c>__init__</c>.
///
/// WHAT DISCRIMINATES: fill() prints 6, ch[2] prints 7.
/// </summary>
[TestFixture]
public class RepeatedNoneListTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("repeated-none-list"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("repeated-none-list"));
    }

    private static ArduinoUnoSimulation FullRun(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 4000);
        return uno;
    }

    [Test]
    public void ARuntimeIndexStore_ReadsTheSlot()
    {
        FullRun(_session).Serial.Should().ContainLine("6",
            because: "[None]*18 is 18 SRAM slots, so a range(18) store of offset at 6 reads 6");
    }

    [Test]
    public void AFieldRepeatedByLenSelf_KeepsWhatWasStored()
    {
        FullRun(_session).Serial.Should().ContainLine("7",
            because: "self.ch = [None]*len(self) is 4 slots, so ch[2] = 7 stores 7, not bit 2 of a byte");
    }

    [Test]
    public void TheSameThroughThePythonFrontEnd()
    {
        FullRun(_pySession).Serial.Text.Should().Contain("6\n7\nEND\n",
            because: "both front ends must lay out [None]*n as a fixed array");
    }
}
