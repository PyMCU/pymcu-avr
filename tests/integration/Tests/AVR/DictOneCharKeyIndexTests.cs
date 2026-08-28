using Avr8Sharp.TestKit.Boards;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// The loud half of PyMCU#215, in a program of its own.
///
/// `d[k]` with a one-character key held in a name refused to build, "KeyError: 257", naming a
/// number the program never wrote. Same compiler site as `d.get(k, default)`; the only
/// difference is whether a default was passed.
///
/// Separate from dict-one-char-key because a failed build stops everything after it: sharing a
/// program with the silent rows means the silent rows can never be measured, and the fixture
/// would then only ever demonstrate the half that was already obvious.
/// </summary>
[TestFixture]
public class DictOneCharKeyIndexTests
{
    private static SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() =>
        _session = new SimSession(PymcuCompiler.BuildFixture("dict-one-char-key-index"));

    [Test]
    public void IndexingWithAOneCharKeyInANameBuildsAndFindsTheEntry()
    {
        var uno = _session.Reset();
        uno.RunToBreak();
        uno.Data[0x3E].Should().Be(70, "d[k] with k = \"a\" must find 70, and must build at all");
    }
}
