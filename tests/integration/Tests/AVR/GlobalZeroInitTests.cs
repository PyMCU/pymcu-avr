// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Zero-initialized mutable globals must be written at startup. The frontend
/// skipped them trusting BSS zeroing, but a register-homed global (R2-R15)
/// is outside the BSS loop and AVR registers power up undefined: on a real
/// Uno the global started with bootloader leftovers. The emulator zeroes
/// registers on reset, so this test dirties R2-R15 first to reproduce the
/// hardware condition.
/// </summary>
[TestFixture]
public class GlobalZeroInitTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("global-zero-init"));

    [Test]
    public void RegisterHomedZeroGlobal_IsZeroAtFirstRead()
    {
        var uno = _session.Reset();
        for (int r = 2; r <= 15; r++)
            uno.Data[r] = 0xAA;
        uno.RunUntilSerial(uno.Serial, "D", maxMs: 2000);
        uno.Serial.Text.Should().Be("c0=0\nc2=14\nb=101\nD");
    }
}
