using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/import-alias.
///
/// `from pymcu.hal.console import print as p` and `import pymcu.hal.console as console`
/// both name a builtin the compiler lowers itself rather than a symbol in the module.
/// Both used to be mangled to `pymcu_hal_console_print` and fail at the call site with
/// an error that named that symbol and never mentioned the alias (PyMCU#66).
/// </summary>
[TestFixture]
public class ImportAliasTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("import-alias"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 500);
        return uno;
    }

    [Test]
    public void AliasedBuiltin_Prints()
    {
        var uno = Boot();
        uno.Serial.Text.Should().Contain("11\n", "p(a) is print(a) under an alias");
    }

    [Test]
    public void BuiltinThroughAnAliasedModule_Prints()
    {
        var uno = Boot();
        uno.Serial.Text.Should().Contain("22\n", "console.print(b) reaches the same builtin");
    }
}
