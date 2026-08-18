// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// UART(115200) must configure U2X0 double speed with UBRR=16 (115942 baud,
/// +0.64%). The old UBRR=8 at 16x oversampling ran at 111111 baud, -3.5%:
/// transmit worked (the far end resynchronizes per start bit) but the AVR's
/// receiver dropped every byte on real silicon, which the emulator cannot
/// show because it does not model baud mismatch. This pins the registers.
/// </summary>
[TestFixture]
public class Uart115200U2xTests
{
    private SimSession _session = null!;

    private const int UCSR0A = 0xC0;
    private const int UBRR0L = 0xC4;
    private const int UBRR0H = 0xC5;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("uart-115200-u2x"));

    [Test]
    public void Baud115200_UsesDoubleSpeedWithUbrr16()
    {
        var uno = _session.Reset();
        uno.RunUntilSerialBytes(uno.Serial, 1, maxMs: 50);
        uno.Data[UBRR0L].Should().Be(16, "UBRR=16 with U2X0 gives 115942 baud, +0.64%");
        uno.Data[UBRR0H].Should().Be(0);
        ((int)(uno.Data[UCSR0A] & 0x02)).Should().Be(0x02, "U2X0 double speed is required for the +0.64% divisor");
    }
}
