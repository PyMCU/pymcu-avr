using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/in-over-named-list (PyMCU#85).
///
/// `x in data` over a name bound to a list did not compile, refused by a message that
/// recommends that very spelling. Both a hit and a miss are checked: a membership test that
/// always answers true would pass on the hit alone.
/// </summary>
[TestFixture]
public class InOverNamedListTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("in-over-named-list"));

    [Test]
    public void AValueInTheListIsFound()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().StartWith("yes\n", "seed is 2 and the list holds 1, 2, 3, 4");
    }

    [Test]
    public void AValueOutsideTheListIsNotFound()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Contain("yes\nnot in plain\ndone\n", "2 is not in [7, 8]");
    }
}
