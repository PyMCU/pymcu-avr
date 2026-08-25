using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/array-runtime-init (PyMCU#81).
///
/// An array initialized from run-time values kept none of them: only the constants were
/// stored. The elements are read back one by one before sum() so the transcript says which
/// half is wrong if this ever comes back.
/// </summary>
[TestFixture]
public class ArrayRuntimeInitTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("array-runtime-init"));

    private string Boot()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        return uno.Serial.Text;
    }

    [Test]
    public void TheElementsAreInTheArray()
    {
        Boot().Should().StartWith("3\n4\n", "both slots hold what the initializer computed");
    }

    [Test]
    public void SumAddsThemAll()
    {
        Boot().Should().Contain("3\n4\n7\ndone\n");
    }
}
