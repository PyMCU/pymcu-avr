// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// uint32(float) must truncate the value, not leak float bits. Copy
/// propagation forwarded through the same-width FLOAT->UINT32 cast copy so
/// the conversion vanished, and the AVR float-binary path clobbered the
/// high word of the __fixsfsi result (MOV pair instead of MOVW swap), so a
/// 32-bit destination received the low word duplicated. Caught printing
/// 3.25 on a real Uno.
/// </summary>
[TestFixture]
public class FloatToUintCastTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("float-to-uint-cast"));

    [Test]
    public void FloatCasts_TruncateInsteadOfLeakingBits()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "D", maxMs: 4000);
        uno.Serial.Text.Should().Be("325\n325\n3\n3\n40000\nD");
    }
}
