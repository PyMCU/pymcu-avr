// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// tone() prescaler buckets must respect the 8-bit OCR2A floor: each
/// prescaler N reaches down to 31250/N Hz at 16 MHz in CTC toggle mode. The
/// old thresholds were 4x too low, so OCR2A overflowed and clamped: tone(440)
/// played 488 Hz, tone(1000) 3906 Hz, tone(8000) 31250 Hz - measured on a
/// real Uno with a logic analyser. After the fix the same requests measured
/// 440.1 / 999.9 / 7999.0 Hz.
/// </summary>
[TestFixture]
public class ToneFreqMapTests
{
    private SimSession _session = null!;

    private const int TCCR2B = 0xB1;
    private const int OCR2A  = 0xB3;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("tone-freq-map"));

    [Test]
    public void Buckets_RespectTheOcr2aFloor()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "A", maxMs: 100);
        ((int)(uno.Data[TCCR2B] & 0x07)).Should().Be(0x02, "8000 Hz: prescaler 8");
        ((int)uno.Data[OCR2A]).Should().Be(124, "16M/(2*8*125) = 8000.0 Hz");
        uno.RunUntilSerial(uno.Serial, "B", maxMs: 100);
        ((int)(uno.Data[TCCR2B] & 0x07)).Should().Be(0x03, "1000 Hz: prescaler 32");
        ((int)uno.Data[OCR2A]).Should().Be(249, "16M/(2*32*250) = 1000.0 Hz");
        uno.RunUntilSerial(uno.Serial, "C", maxMs: 100);
        ((int)(uno.Data[TCCR2B] & 0x07)).Should().Be(0x05, "440 Hz: prescaler 128");
        ((int)uno.Data[OCR2A]).Should().Be(141, "16M/(2*128*142) = 440.1 Hz");
    }
}
