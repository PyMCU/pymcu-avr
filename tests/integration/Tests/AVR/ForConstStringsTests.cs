// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A `for` unrolled over constant strings binds the loop variable to each
/// text. The element folder had already reduced the literal to its interned
/// id, so the name stored 256/257 and print wrote numbers where the names
/// were meant (oracle probes 009/010).
/// </summary>
[TestFixture]
public class ForConstStringsTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("for-const-strings"));

    [Test]
    public void StringElements_PrintTheirTextNotTheInternedId()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 3000);
        uno.Serial.Text.Should().Be("PD2\nPD3\n2\nD2\n3\nD3\nEND\n");
    }
}
