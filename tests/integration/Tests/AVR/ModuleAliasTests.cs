using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/module-alias — aliased and comma-separated
/// module imports on the MicroPython layer (<c>import machine as m, time as t</c>).
/// m.UART / m.Pin / t.sleep_ms used to mangle to undefined symbols; reaching the
/// "MA" banner over UART proves the alias resolves to the real module and the
/// constructor, member and method calls all compile and run.
/// </summary>
[TestFixture]
public class ModuleAliasTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("module-alias"));

    [Test]
    public void AliasedModules_ResolveAndRun()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, s => s.Contains("MA"), maxMs: 300);
        uno.Serial.Text.Should().Contain("MA", "m.UART resolves to machine's UART and writes the banner");
    }
}
