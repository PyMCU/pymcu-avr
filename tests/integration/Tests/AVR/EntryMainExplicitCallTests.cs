using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/entry-main-explicit-call (PyMCU#301): a module-level `main()` says where the entry
/// point's body runs. The call used to be dropped, which ran the whole module level first, so
/// `print("B"); main(); print("C")` came out B, C, A -- main's own output last, with nothing
/// reported. CPython prints B, A 3, C.
/// </summary>
[TestFixture]
public class EntryMainExplicitCallTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("entry-main-explicit-call"));

    [Test]
    public void MainsBodyRunsWhereTheCallIsWritten()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 600);
        uno.Serial.Text.Should().Be("B\nA 3\nC\nEND\n",
            "the statements below the call run after main's body, as they do in CPython");
    }
}
