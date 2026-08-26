using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/join-field-receiver (PyMCU#191).
///
/// `.join()` on a separator held in a field was refused, by a message telling the reader to do
/// what the program already did. The condition was the receiver: a member access was never
/// looked at, so the assignment lowering declined and the expression fell through.
///
/// Four discriminators, four controls, and the split is stated per test rather than left to be
/// counted. The controls carry the argument: without them "a one-character field prints its
/// character code" reads as a string bug rather than a field bug.
///
/// Measured: the whole fixture is REFUSED by the compiler at da1a6b7e (`call to undefined
/// function 'flat_sep_join'`) and compiles at 576dee6e.
/// </summary>
[TestFixture]
public class JoinFieldReceiverTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
        => _session = new SimSession(PymcuCompiler.BuildFixture("join-field-receiver"));

    private static string Output()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, ", .\n", maxMs: 800);
        return uno.Serial.Text.Replace("\r\n", "\n");
    }

    // DISCRIMINATOR. The bound the issue's title gets wrong: it says "nested field", and a
    // single level was refused identically. A test set built from the title will not have this.
    [Test]
    public void ASingleLevelFieldReceiverJoins()
        => Output().Should().StartWith("x, y\n", "o.sep is a member access too, and the nesting was never the condition");

    // DISCRIMINATOR. The shape the issue actually reports.
    [Test]
    public void ANestedFieldReceiverJoins()
        => Output().Should().StartWith("x, y\nx, y\n", "o.inner.sep resolves through two levels");

    // DISCRIMINATOR. A one-character separator in a field, which took a second fix to reach:
    // its interned id is its own character code, so the text was lost before the receiver was
    // ever consulted.
    [Test]
    public void AOneCharacterFieldSeparatorJoins()
        => Output().Should().Contain("\nx,y\n", "a one-character separator is still a separator");

    // DISCRIMINATOR. The constructor being @inline is not the condition either.
    [Test]
    public void APlainNonInlineConstructorJoinsToo()
        => Output().Should().StartWith("x, y\nx, y\nx,y\nx, y\n", "the fourth line is the plain __init__");

    // CONTROL. Always compiled. Its job is to localise the failure to the receiver.
    [Test]
    public void APlainLocalReceiverStillJoins()
        => Output().Should().Contain("x,y\n", "the local receiver is the case that always worked");

    // CONTROL. The literal at the call site, the form the old message told readers to write.
    [Test]
    public void ALiteralReceiverStillJoins()
        => Output().Should().EndWith("x,y\n,\n, .\n", "a literal separator was never in question");

    // CONTROL for the character-code degradation: reading the field directly must give the
    // text, not the number 44.
    [Test]
    public void AOneCharacterFieldReadsAsItsText()
        => Output().Should().Contain("\n,\n", "o.sep is \",\", not 44");

    // CONTROL. The multi-character field always read correctly; it is the matched pair that
    // makes the one-character case legible as a length problem.
    [Test]
    public void AMultiCharacterFieldReadsAsItsText()
        => Output().Should().EndWith(", .\n", "\", \" was never degraded");
}
