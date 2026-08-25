using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/module-array-survives (PyMCU#146).
///
/// A module-level array reached only through subscripts lost its reservation, and other slots
/// were handed its bytes. Both a single element and the sum are checked: the element alone
/// could survive a partial overwrite, and the sum alone would not say which end was corrupted.
/// </summary>
[TestFixture]
public class ModuleArraySurvivesTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("module-array-survives"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        return uno.Serial.Text;
    }

    [Test]
    public void AnElementHoldsWhatWasWritten()
    {
        Boot().Should().StartWith("70\n", "buf[6] is 10 + 6 * 10");
    }

    [Test]
    public void EveryElementSurvives()
    {
        Boot().Should().Contain("70\n360\ndone\n", "10 + 20 + ... + 80");
    }
}
