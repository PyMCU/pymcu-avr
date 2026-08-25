using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/module-instance-field (PyMCU#124).
///
/// A module-level object mutated from one function and read from another read 0. The plain
/// global in the same shape was always correct and is checked alongside, so a future failure
/// says whether the instance path broke or the whole fixture did.
/// </summary>
[TestFixture]
public class ModuleInstanceFieldTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("module-instance-field"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        return uno.Serial.Text;
    }

    [Test]
    public void TheFieldKeepsWhatAnotherFunctionWrote()
    {
        Boot().Should().StartWith("77\n", "setup() assigned 77 before main() read it");
    }

    [Test]
    public void ThePlainGlobalStillWorks()
    {
        Boot().Should().Contain("77\n5\ndone\n");
    }
}
