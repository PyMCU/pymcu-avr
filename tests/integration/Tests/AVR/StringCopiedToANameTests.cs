using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/string-copied-to-a-name (PyMCU#209).
///
/// `a = "abc"` then `b = a` then `print(b)` printed 256 -- the string's interned id, written as
/// a decimal. Two lines, the most ordinary construct there is, and a clean build. The number
/// depends on how many other strings the program has, which is what makes it read as noise.
///
/// Four discriminators and one control. Measured: on the previous compiler the image holds only
/// `abc` and the firmware writes the numbers 258, 259, 256 and 257 in place of the four strings.
///
/// Length-independent, unlike #211: one character and three characters failed identically, and
/// that is what says the id was recoverable all along and the boundary simply did not look.
/// </summary>
[TestFixture]
public class StringCopiedToANameTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
        => _session = new SimSession(PymcuCompiler.BuildFixture("string-copied-to-a-name"));

    private static string Output()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 800);
        return uno.Serial.Text.Replace("\r\n", "\n");
    }

    // DISCRIMINATOR. The issue's own two-line program.
    [Test]
    public void AStringBoundToASecondNameKeepsItsText()
        => Output().Should().StartWith("abc\n", "b = a holds the text, not the id");

    // DISCRIMINATOR. One character, to show the defect is not #211's: it failed the same way at
    // both lengths, which is what places it at the boundary rather than in the encoding.
    [Test]
    public void AOneCharacterStringSurvivesTheCopyToo()
        => Output().Should().Contain("\nx\n", "one character and three failed identically here");

    // DISCRIMINATOR. A module-level constant read into a local.
    [Test]
    public void AModuleConstantCopiedIntoALocalKeepsItsText()
        => Output().Should().Contain("\nhi\n", "c = BANNER is the same copy through a global");

    // DISCRIMINATOR. Through a field of a LOCAL instance. The module-level spelling already
    // worked before this fix, so writing it that way would have been a control wearing a
    // discriminator's name.
    [Test]
    public void AFieldOfALocalInstanceCopiedIntoALocalKeepsItsText()
        => Output().Should().Contain("\nfld\n", "n = c.name, with c a local");

    // CONTROL. Printing the name the literal was written on always worked. Its job is to place
    // the failure at the copy rather than at print or at the string itself.
    [Test]
    public void PrintingTheOriginalNameStillWorks()
        => Output().Should().EndWith("abc\ndone\n");

    // The failure mode itself: a decimal where a string belongs. Stated separately because the
    // four assertions above would all pass on a compiler that printed the right strings and
    // some spurious numbers as well.
    [Test]
    public void NothingIsPrintedAsANumber()
    {
        var text = Output();

        foreach (var id in new[] { "256", "257", "258", "259" })
            text.Should().NotContain(id, "an interned id printed as a decimal is the whole bug");
    }
}
