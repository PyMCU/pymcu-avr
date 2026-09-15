// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit.Boards;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// `except X as e` binds a bounded exception object (#369).
///
/// The T-flag model carries a type code in R22 and nothing else, so a handler had nothing to
/// bind and the form was refused in the parser. What this fixture holds is the payload
/// alongside the code: one module-level word holding the flash address of the raise's
/// string-literal message, walked by the same shared subroutine that prints every other
/// string. One exception is live at a time, so one word is the whole object.
///
/// The expected lines are CPython's, running the same program. That is the only reason to
/// trust them: a fixture whose expected output is written by hand asserts what the compiler
/// did, not what Python means.
///
/// WHAT DISCRIMINATES: `a:checksum mismatch` and `b:bad value`. The message reaches the
/// handler through three spellings that must agree -- `e.args[0]`, `print(e)`, `str(e)` -- and
/// a store the optimizer deletes gives an empty line rather than an error, which is what this
/// looked like before the word was declared a global: the strings sat in flash, unreferenced.
///
/// The `d:` pair is the second discriminator. A handler binds a name, reads it, re-raises, and
/// the caller binds a DIFFERENT name for the same live exception.
///
/// WHAT IS INVARIANT: every program that binds nothing. It compiles to the bytes it always
/// did, which is measured by the ROM differential rather than here.
/// </summary>
[TestFixture]
public class ExceptionObjectTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("exception-object"));

    [Test]
    public void TheMessageOfARaiseReachesArgsZero()
    {
        // The adafruit_dht simpletest shape: except RuntimeError as error, print(error.args[0]).
        var uno = FullRun();

        uno.Serial.Should().ContainLine("a:checksum mismatch");
    }

    [Test]
    public void PrintingTheBoundNameReadsTheSameMessage()
    {
        var uno = FullRun();

        uno.Serial.Should().ContainLine("b:bad value");
    }

    [Test]
    public void ABoundHandlerCanReRaiseToAHandlerThatBindsADifferentName()
    {
        var uno = FullRun();

        uno.Serial.Should().ContainLine("d:inner fault");
        uno.Serial.Should().ContainLine("d:outer fault");
    }

    [Test]
    public void TwoBoundNamesAreLiveAtOnceInANestedTry()
    {
        var uno = FullRun();

        uno.Serial.Should().ContainLine("e:index fault");
        uno.Serial.Should().ContainLine("c=2");
    }

    [Test]
    public void IsinstanceOnTheBoundNameAnswersBothWays()
    {
        // 1 for the type that was raised and 0 for one that was not. A test that only checked
        // the true case would pass on a fold to a constant 1.
        var uno = FullRun();

        uno.Serial.Should().ContainLine("f=1,0");
    }

    private ArduinoUnoSimulation FullRun(int maxMs = 5000)
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "DONE\n", maxMs: maxMs);
        return uno;
    }
}
