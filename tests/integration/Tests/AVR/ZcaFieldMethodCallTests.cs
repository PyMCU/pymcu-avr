// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A user class whose method calls a method on one of its own fields. Such a body
/// cannot become a shared subroutine — the field would arrive as a plain number,
/// and a number has no methods — so it must be expanded per call site. It used to
/// be outlined regardless, which failed the whole build with an undefined
/// 'self__pin_value' even when the method was never called.
/// </summary>
[TestFixture]
public class ZcaFieldMethodCallTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("zca-field-method-call"));

    [Test]
    public void MethodOnAFieldReadsTheRealPin()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "off=0\n", maxMs: 3000);
        uno.Serial.Text.Should().Contain("on=1\noff=0\n");
    }

    private ArduinoUnoSimulation Sim() => _session.Reset();
}
