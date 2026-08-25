using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/str-class-field (the class-field half of PyMCU#80).
///
/// A string in a field printed as 256, its interned id. Two instances are used because two
/// consecutive ids (256 and 257) is what the bug looked like, and a fix that printed one
/// instance's text for both would pass a single-instance test.
/// </summary>
[TestFixture]
public class StrClassFieldTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("str-class-field"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        return uno.Serial.Text;
    }

    [Test]
    public void AFieldReadFromInsideAndOutsideBothPrintTheText()
    {
        Boot().Should().StartWith("hi\nhi\n", "self.n and o.n are the same string");
    }

    [Test]
    public void EachInstanceKeepsItsOwnText()
    {
        Boot().Should().Contain("hi\nhi\nbye\ndone\n");
    }
}
