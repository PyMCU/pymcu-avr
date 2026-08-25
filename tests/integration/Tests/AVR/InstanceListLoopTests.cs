using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/instance-list-loop (PyMCU#100).
///
/// `for o in objs` over a list of instances read every field as zero. The two elements are
/// deliberately given different values: a loop that bound both iterations to the same instance
/// would print the same number twice and still look plausible.
/// </summary>
[TestFixture]
public class InstanceListLoopTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("instance-list-loop"));

    [Test]
    public void EachElementKeepsItsOwnField()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Contain("5\n6\ndone\n", "the elements hold 4 and 5, and g() adds one to each");
    }
}
