using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/imported-module-init (PyMCU#129).
///
/// Only the ENTRY file's module level was executed, so anything an imported module set up at
/// its top level arrived as zero: a plain `n: uint16 = 7`, and an object built as `c = C(5)`.
/// The storage and the later writes were real, so a counter in an imported module counted
/// 0, 1, 2 instead of 5, 6, 7: it compiled, it ran, and it was wrong by a constant.
///
/// Against the unfixed compiler this prints 0, 0, 1, 2.
/// </summary>
[TestFixture]
public class ImportedModuleInitTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("imported-module-init"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 3000);
        return uno.Serial.Text;
    }

    [Test]
    public void APlainGlobalInAnImportedModuleKeepsItsInitializer()
    {
        Boot().Should().StartWith("7\n", "n: uint16 = 7 lives in counter.py, not in main.py");
    }

    [Test]
    public void AnObjectBuiltAtAnImportedModulesTopLevelKeepsItsConstructorArgument()
    {
        Boot().Should().Contain("7\n5\n", "C(5) must start at 5, not at 0");
    }

    [Test]
    public void LaterMutationsCountFromTheRealStartingValue()
    {
        Boot().Should().EndWith("5\n6\n7\ndone\n");
    }
}
