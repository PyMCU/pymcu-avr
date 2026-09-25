// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The MicroPython twin of <see cref="CompatCpUart115200U2xTests"/>. machine.UART(0, 115200)
/// has to reach the registers the native UART(115200) reaches, U2X0 with UBRR=16.
///
/// Both compat layers carry the rate through a parameter of declared width, and 115200 does
/// not fit a uint16: a layer that declares one hands the HAL 49664 and the part runs at
/// 50000 baud.
///
/// What let that ship was not the declaration, it was the coverage. The native path had a
/// test and the layers did not. CircuitPython was caught only sideways, by a buffer test
/// losing received bytes, and MicroPython was broken in silicon at the same time with every
/// suite green, because nothing anywhere asserted its registers. The next compat layer
/// needs its own case here.
/// </summary>
[TestFixture]
public class CompatMpUart115200U2xTests
{
    private SimSession _session = null!;

    private const int UCSR0A = 0xC0;
    private const int UBRR0L = 0xC4;
    private const int UBRR0H = 0xC5;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-mp-uart-115200-u2x"));

    [Test]
    public void MpBaud115200_UsesDoubleSpeedWithUbrr16()
    {
        var uno = _session.Reset();
        uno.RunUntilSerialBytes(uno.Serial, 1, maxMs: 50);
        uno.Data[UBRR0L].Should().Be(16, "UBRR=16 with U2X0 gives 115942 baud, +0.64%");
        uno.Data[UBRR0H].Should().Be(0);
        ((int)(uno.Data[UCSR0A] & 0x02)).Should().Be(0x02,
            "U2X0 double speed is required for the +0.64% divisor; UBRR=19 without it is 50000 baud, "
            + "which is what 115200 truncated to a uint16 asks for");
    }
}
