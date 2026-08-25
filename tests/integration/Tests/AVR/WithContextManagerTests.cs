using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/with-context-manager (PyMCU#101).
///
/// `with o as v:` bound v to nothing and read every field as zero; `with V(s) as v:` did not
/// build, reporting v as never assigned on the line that assigns it. The fixture runs both
/// spellings in one program so neither can regress without the other noticing.
/// </summary>
[TestFixture]
public class WithContextManagerTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("with-context-manager"));

    [Test]
    public void BothSpellingsSeeTheFieldTheManagerHolds()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        uno.Serial.Text.Should().Contain("3\n3\ndone\n", "GPIOR0 reads 0, so the field holds 3 in both blocks");
    }
}
