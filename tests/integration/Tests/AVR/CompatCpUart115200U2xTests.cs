// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The compat-layer twin of <see cref="Uart115200U2xTests"/>. busio.UART(baudrate=115200)
/// has to reach the registers the native UART(115200) reaches, U2X0 with UBRR=16.
///
/// The native path was pinned and this one was not, and this one is where the rate travels
/// through a declared parameter: 115200 does not fit a uint16, so a layer that declares one
/// hands the HAL 49664 and the part is configured for 50000 baud. Nothing says so. The
/// emulator does not model a baud mismatch either, so from above it showed up as a receive
/// test losing bytes, which is three failures that never name the rate.
/// </summary>
[TestFixture]
public class CompatCpUart115200U2xTests
{
    private SimSession _session = null!;

    private const int UCSR0A = 0xC0;
    private const int UBRR0L = 0xC4;
    private const int UBRR0H = 0xC5;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("compat-cp-uart-115200-u2x"));

    [Test]
    public void CompatBaud115200_UsesDoubleSpeedWithUbrr16()
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
