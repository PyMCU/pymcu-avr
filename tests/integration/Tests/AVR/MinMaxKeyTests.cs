// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// `min(a, b, key=f)` and `max(...)`, PyMCU/PyMCU#190. The keyword used to reach the reader as
/// `Unknown Expression type: KeywordArgExpr`, the name of a class inside the compiler, because
/// min and max are lowered before the general keyword path and nothing downstream knew the
/// node. It compiles now.
///
/// These are assertions on what the running program writes rather than on the build, because a
/// build-success assertion passes on a key that is ignored: `max(3, 1, key=rank)` builds and
/// answers 3 whether the key was applied or not, and 3 is the wrong answer.
/// </summary>
[TestFixture]
public class MinMaxKeyTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("minmax-key"));

    private ArduinoUnoSimulation Sim() => _session.Reset();

    /// <summary>
    /// `rank` inverts, so every line answers the opposite of the plain comparison. That is what
    /// discriminates: an ignored key gives 3 and 1, which is exactly what the two control lines
    /// below print for the same operands.
    /// </summary>
    [Test]
    public void TheKeyDecidesWhichOperandWins()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 500);

        uno.Serial.Text.Should().Contain("KEY\n1\n3\n");
    }

    [Test]
    public void WithoutAKeyTheOperandsAreComparedDirectly()
    {
        // The control, and the invariant: the plain lowering is the one every other program in
        // the corpus uses, and the key path must not have been wired into it.
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 500);

        uno.Serial.Text.Should().Contain("1\n3\n3\n1\n");
    }

    /// <summary>
    /// The sequence form over a fixed-size array, where the winner is neither the first nor the
    /// last element, so an off-by-one in the fold shows up as a different number.
    /// </summary>
    [Test]
    public void AKeyAppliesToTheSequenceFormToo()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 500);

        uno.Serial.Text.Should().Contain("1\n70\n");
    }

    /// <summary>
    /// One key call per operand, 2 + 2 + 3, which is the count CPython makes. An operand read
    /// twice (once to compute its key, once as the result) would double some of them, and a
    /// running winner whose key is recomputed at each step would make 2 + 2 + 4.
    /// </summary>
    [Test]
    public void EachOperandIsPassedToTheKeyExactlyOnce()
    {
        var uno = Sim();
        uno.RunUntilSerial(uno.Serial, "END\n", maxMs: 500);

        uno.Serial.Text.Should().Contain("70\n7\nEND\n");
    }
}
