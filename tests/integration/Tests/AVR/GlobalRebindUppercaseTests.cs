// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A module global written through `global`, PyMCU/PyMCU#220. `N = 10` is ALL CAPS, and ALL
/// CAPS means "constant" by convention here, which is what gives the name no storage so every
/// read folds the initializer. The write then produced a `copy` whose DESTINATION was the
/// literal 10: it went nowhere, and both reads answered 10 where CPython answers 10 then 20.
///
/// Read from the serial line rather than from the build, because the whole defect is a value:
/// the program built cleanly and reported nothing on the way to being wrong.
///
/// The lowercase spelling of the same program has always worked and is here beside it, so a
/// fix that helped one and not the other cannot pass. COUNT is the control in the other
/// direction: ALL CAPS and never written, so it must still fold.
/// </summary>
[TestFixture]
public class GlobalRebindUppercaseTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("global-rebind-uppercase"));

    private string Transcript()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 500);
        return uno.Serial.Text;
    }

    /// <summary>
    /// The two reads of the uppercase name, before and after the write. 10 then 20; the defect
    /// answered 10 twice, so the second number is the whole test.
    /// </summary>
    [Test]
    public void AnUppercaseGlobalKeepsTheValueItWasGiven()
    {
        Transcript().Should().Contain("GR\n10\n20\n");
    }

    [Test]
    public void TheLowercaseSpellingStillWorks()
    {
        Transcript().Should().Contain("20\n7\n14\n");
    }

    /// <summary>
    /// An uppercase name that is never written is still a constant. The convention is what
    /// makes it fold, and only a name the module writes stops being one.
    /// </summary>
    [Test]
    public void AnUppercaseNameThatIsNeverWrittenIsUntouched()
    {
        Transcript().Should().Contain("14\n30\nEND\n");
    }
}
