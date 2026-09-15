using System;
using System.Linq;
using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/field-truthiness-operators (PyMCU#385): every operator that asks a FIELD for a
/// truth value asks the field's class.
///
/// fixtures/instance-field-truthiness covers `if` and `while not`. The rewrite is reached from
/// four more places -- `not`, the operands of `and` and `or`, a conditional expression and
/// `bool()` -- and the conditional expression missed it for a reason of its own: it asked
/// whether the condition was None BEFORE rewriting, and a field whose class collapsed onto the
/// field below it has no value under its own name, so it read as None and the expression
/// answered with its false side without ever reaching the class.
///
/// Every class in the fixture answers the OPPOSITE of the scalar it collapses to, so a
/// condition decided by storage and a condition decided by the class never agree and no line
/// can pass by accident. `late` is an owner whose field's class is written BELOW it, which the
/// scan reaches before it has seen that class.
/// </summary>
[TestFixture]
public class FieldTruthinessOperatorsTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("field-truthiness-operators"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("field-truthiness-operators"));
    }

    private static string[] Lines(SimSession s)
    {
        var uno = s.Reset();
        uno.RunMilliseconds(400);
        return uno.Serial.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                              .Select(l => l.Trim()).ToArray();
    }

    // Measured by running the same file under CPython, not read off the firmware.
    private static readonly string[] Cpython =
    {
        "if-true 1", "if-false 0", "not 1", "and 1", "or 1", "ternary 1", "bool 1",
        "bool-dunder 1", "name 1", "name-bool 1", "late 1", "wait 2", "wait 0", "END",
    };

    [Test]
    public void EveryOperatorAsksTheFieldsClass()
        => Lines(_session).Should().Equal(Cpython,
            "not, and, or, a conditional expression and bool() are truth tests like `if`");

    [Test]
    public void TheSameThroughThePythonFrontEnd()
        => Lines(_pySession).Should().Equal(Cpython);

    [Test]
    public void AConditionalExpressionOnAFieldIsNotReadAsNone()
        => Lines(_session).Should().Contain("ternary 1",
            "the protocol rewrite runs before the None check, as it does for an `if`");

    [Test]
    public void AFieldWhoseClassIsWrittenBelowItsOwnerStillAnswers()
        => Lines(_session).Should().Contain("late 1",
            "the scan reaches the owner before the class, and the field is one either way");

    [Test]
    public void TheConditionIsReReadEveryTurn()
        => Lines(_session).Should().ContainInOrder(new[] { "wait 2", "wait 0" },
            "__len__ counts its calls and answers 0, 0, then 1");
}
