using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/module-instance-read (PyMCU#126 and PyMCU#127).
///
/// Two module-level objects: one only ever read from another function, one written through a
/// method called one level below main. Both answered 0. They are in the same fixture because
/// they are the same fault seen from the read side and the write side.
/// </summary>
[TestFixture]
public class ModuleInstanceReadTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("module-instance-read"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        return uno.Serial.Text;
    }

    [Test]
    public void AFieldOnlyEverReadStillHoldsWhatTheConstructorPutThere()
    {
        Boot().Should().StartWith("5\n", "cfg.n is never assigned outside __init__");
    }

    [Test]
    public void AWriteThroughAMethodOneLevelDownIsSeen()
    {
        Boot().Should().Contain("5\n77\ndone\n", "touch() calls obj.mark(), which sets n to 77");
    }
}
