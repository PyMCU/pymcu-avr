// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Servo.write(degrees) must land on the exact OCR value: tick = 0.5 us in
/// Timer1 mode 14 at prescaler 8, so 90 deg = 1500 us = OCR 2999. The old
/// degrees*11 approximation gave OCR 2989 (1495 us at 90 deg, 1990 us at
/// 180 deg), confirmed on a real Uno with a logic analyser; the exact map is
/// degrees*100//9.
/// </summary>
[TestFixture]
public class ServoAngleMapTests
{
    private SimSession _session = null!;

    private const int OCR1AL = 0x88;
    private const int OCR1AH = 0x89;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("servo-angle-map"));

    private static int Ocr1a(ArduinoUnoSimulation uno) => uno.Data[OCR1AL] | (uno.Data[OCR1AH] << 8);

    [Test]
    public void Angles_MapToExactPulseTicks()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "A", maxMs: 100);
        Ocr1a(uno).Should().Be(1999, "0 deg = 1000 us");
        uno.RunUntilSerial(uno.Serial, "B", maxMs: 100);
        Ocr1a(uno).Should().Be(2999, "90 deg = 1500 us");
        uno.RunUntilSerial(uno.Serial, "C", maxMs: 100);
        Ocr1a(uno).Should().Be(3999, "180 deg = 2000 us");
    }
}
