using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/outline-dunders (PyMCU#98).
///
/// Four dunders marked @outline: two used to answer wrongly and two used to fail the build.
/// Each is checked with a value that could not come out of the zero the operator fell back
/// to, so a fallback cannot pass by coincidence.
/// </summary>
[TestFixture]
public class OutlineDundersTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("outline-dunders"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        return uno.Serial.Text;
    }

    [Test]
    public void LenAndGetItemAnswerFromTheBody()
    {
        Boot().Should().StartWith("6\n7\n", "len is a + 1 and b[2] is a + 2, with a starting at 5");
    }

    [Test]
    public void SetItemWritesTheFieldTheNextReadSees()
    {
        Boot().Should().Contain("6\n7\n11\n", "b[1] = 10 sets a to 11, and b[0] reads it back");
    }

    [Test]
    public void ContainsAnswersFromTheBody()
    {
        Boot().Should().Contain("in\ndone\n");
    }
}
