using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/stdlib-bare-imports.
///
/// `import math` and `from random import randint` are the spellings every Python program
/// uses; both used to fail as "Module not found" with advice to install a library that
/// does not exist, because the modules ship as pymcu.math and pymcu.random (PyMCU#58).
///
/// The assertions are on results, not on the build: a redirect that resolved the module
/// but bound the wrong symbols would still compile.
/// </summary>
[TestFixture]
public class StdlibBareImportsTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;

    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("stdlib-bare-imports"));

    private ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunToBreak();
        return uno;
    }

    [Test]
    public void MathThroughItsBareName_Computes()
    {
        var uno = Boot();
        uno.Data[Gpior0Addr].Should().Be(50, "map_range(512, 0, 1023, 0, 100) is 50");
    }

    [Test]
    public void RandintWithAOneValueRange_IsThatValue()
    {
        var uno = Boot();
        uno.Data[Gpior1Addr].Should().Be(3, "randint(3, 3) includes both ends, so it is 3");
    }

    [Test]
    public void SeededRandint_StaysInRange()
    {
        var uno = Boot();
        uno.Data[Gpior2Addr].Should().BeInRange(0, 10, "randint(0, 10) is inclusive of both ends");
    }
}
