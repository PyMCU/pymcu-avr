// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Exception-model edges: bare re-raise, Exception as catch-all (used to
/// match nothing under the exact-code compare), user-defined exception
/// classes (used to be an undefined-symbol link error), subclass raising,
/// and bare except (used to be a syntax error). Verified on a real Uno.
/// </summary>
[TestFixture]
public class ExceptionEdgesTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("exception-edges"));

    [Test]
    public void AllEdges_MatchPython()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "END", maxMs: 2000);
        uno.Serial.Text.Should().Contain("a=42\nb=55\nc=77\nd=88\ne=2\nf=66");
    }
}
