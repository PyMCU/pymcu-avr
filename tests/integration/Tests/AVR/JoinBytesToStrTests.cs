// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// str.join lowering: sep.join([...]) over compile-time strings folds to one
/// constant, and ''.join([chr(b) for b in buf]) — the canonical MicroPython/
/// CircuitPython bytes-to-string idiom — becomes a runtime string readable by
/// subscript. Previously both died with a misleading nested-member-access error.
/// </summary>
[TestFixture]
public class JoinBytesToStrTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("join-bytes-to-str"));

    [Test]
    public void ConstFold_AndBytesToString_BothWork()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "D", maxMs: 4000);
        uno.Serial.Text.Should().Be("ab-cd\nHola\nD");
    }
}
