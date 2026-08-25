using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/imported-class (PyMCU#130).
///
/// A class in its own file could not be constructed anywhere, including from a function
/// inside that same file. Putting a class in its own module is the most ordinary thing a
/// program does, so the fixture builds one from the importing file AND from inside the
/// module, and reads a field back as well as calling a method.
/// </summary>
[TestFixture]
public class ImportedClassTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("imported-class"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        return uno.Serial.Text;
    }

    [Test]
    public void AMethodOnTheImportedClassAnswers()
    {
        Boot().Should().StartWith("15\n", "base is 10 and read() adds 5");
    }

    [Test]
    public void AFieldOfTheImportedClassReadsBack()
    {
        Boot().Should().Contain("15\n10\n", "the constructor argument survives");
    }

    [Test]
    public void ConstructingItInsideItsOwnModuleWorksToo()
    {
        Boot().Should().Contain("15\n10\n11\ndone\n", "make() builds a Sensor in sensor.py itself");
    }
}
