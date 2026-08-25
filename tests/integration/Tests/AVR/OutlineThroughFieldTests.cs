using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/outline-through-field (PyMCU#103).
///
/// Adding @outline to a method that calls a method of an instance held in a field failed the
/// build with "call to undefined function 'self_inner_get'". The decorator now asks for a
/// shared body where one is possible and force-inlines where none exists, so the program
/// builds and answers the same as it does undecorated.
/// </summary>
[TestFixture]
public class OutlineThroughFieldTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("outline-through-field"));

    [Test]
    public void TheCallThroughTheFieldAnswers()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Contain("3\n", "GPIOR0 reads 0, the inner adds 1 and the outer adds 2");
    }
}
