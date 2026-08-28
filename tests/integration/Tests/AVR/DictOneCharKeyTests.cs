using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Integration tests for fixtures/dict-one-char-key (PyMCU#215).
///
/// A one-character string has two encodings: as a literal it folds to its character code, and
/// through a name it resolves to its interned id. The dict lookup compared the numbers, so a key
/// held in a name missed its entry. `d[k]` raised KeyError naming 256 and `d.get(k, 1)` silently
/// returned the default.
///
/// `d[k]` and `d.get(k, default)` are ONE site in the compiler, differing only in whether a
/// default was passed, so one fix covers both. The `d[k]` spelling is tested in
/// DictOneCharKeyIndexTests rather than here: unfixed it FAILS THE BUILD, and a failed build in
/// this program would stop every silent row below from being measured at all.
///
/// Every key in the fixture is different, so a lookup that ignored the key entirely would show
/// up rather than pass. No seed: the dict and the keys ARE literals, which is the construct
/// under test, and the defect is in what the fold compares rather than in the folding.
/// </summary>
[TestFixture]
public class DictOneCharKeyTests
{
    private const int Gpior0Addr = 0x3E;
    private const int Gpior1Addr = 0x4A;
    private const int Gpior2Addr = 0x4B;

    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("dict-one-char-key"));

    private static ArduinoUnoSimulation Boot()
    {
        var uno = _session.Reset();
        uno.RunToBreak();
        return uno;
    }

    // DISCRIMINATING. Unfixed this was the default, 1.
    [Test]
    public void GetWithAOneCharKeyInANameFindsTheEntry() =>
        Boot().Data[Gpior0Addr].Should().Be(70, "d.get(k, 1) with k = \"a\" must find 70, not fall to the default");

    // DISCRIMINATING. A different key from the row above, so a lookup that ignored the key and
    // always returned the first entry would show here rather than pass. Unfixed: 1.
    [Test]
    public void ASecondOneCharKeyFindsItsOwnEntry() =>
        Boot().Data[Gpior1Addr].Should().Be(7, "d.get(k, 1) with k = \"b\" must find 7");

    // DISCRIMINATING, and it pins that the defect is per KEY rather than per dict: in
    // {"a": 70, "bb": 9} the one-character key failed while the two-character one worked.
    // 70 + 9 + 1: the present one-char key, the present multi-char key, and an absent key whose
    // default IS the correct answer. That last row looks fine either way and is kept so a fix
    // that made every lookup hit would break it.
    [Test]
    public void AMixedLengthDictResolvesEachKeyOnItsOwn() =>
        Boot().Data[Gpior2Addr].Should().Be(80, "70 for \"a\", 9 for \"bb\", 1 for the absent \"z\"");
}
