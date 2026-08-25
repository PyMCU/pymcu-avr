using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The same run-time-decided string one module further away (PyMCU issue #145): the text is
/// declared in an imported module, whose own module level stores it, and a function of that
/// module rebinds it. The seed is written into GPIOR0 before the run, so both directions are
/// exercised rather than whichever one a zero seed happens to take.
/// </summary>
[TestFixture]
public class StrRuntimeModuleTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("str-runtime-module"));

    private static string RunWithSeed(byte seed)
    {
        var uno = _session.Reset();
        uno.Data[0x3E] = seed;              // GPIOR0
        uno.RunMilliseconds(200);
        return uno.Serial.Text;
    }

    [Test]
    public void SeedBelowThreshold_ReadsTheModuleInitializer()
        => RunWithSeed(0).Should().Contain("SM\nidle\n");

    [Test]
    public void SeedAboveThreshold_ReadsWhatTheModuleFunctionWrote()
        => RunWithSeed(20).Should().Contain("SM\nrunning\n");
}
