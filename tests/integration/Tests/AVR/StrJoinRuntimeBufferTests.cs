// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// `s = "".join([chr(b) for b in buf])` builds a runtime string and print()
/// streams it. print()'s string writer takes a parameter also named `s`, which
/// the indexed-load path resolved to the module-level `main.s` instead -- the
/// callee ignored its argument, so every string write emitted the buffer and
/// the newline plus "END" never arrived (oracle probe 070).
/// </summary>
[TestFixture]
public class StrJoinRuntimeBufferTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("str-join-runtime-buffer"));

    [Test]
    public void PrintsTheJoinedTextThenEnd()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 3000);
        uno.Serial.Text.Should().Be("ABC\nEND\n");
    }
}
