// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;
using Avr8Sharp.TestKit;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// A float operation's second operand goes straight into the register pair the
/// soft-float routine reads it from.
///
/// The GCC AVR float ABI puts the first argument in R25:R22 and the second in
/// R21:R18. Both operands used to be loaded into R22:R25, the second one moved
/// four registers down, and the first stacked across the move — four PUSHes,
/// four LDIs, four MOVs and four POPs where four instructions will do. Every
/// float operation in every program paid it; printing one float paid it five
/// times (PyMCU/pymcu-avr#24).
///
/// An integer second operand keeps the long way: converting it calls
/// __floatsisf, which both reads and returns R22:R25, so the first operand
/// still has to survive on the stack. The fixture ends with one of those.
/// </summary>
[TestFixture]
public class FloatArgNoShuffleTests
{
    private SimSession _session = null!;

    [OneTimeSetUp]
    public void BuildFirmware() => _session = new SimSession(PymcuCompiler.BuildFixture("float-arg-no-shuffle"));

    [Test]
    public void FloatOperations_PrintTheSameValues_AsBefore()
    {
        var uno = _session.Reset();
        uno.RunUntilSerial(uno.Serial, "D", maxMs: 4000);
        uno.Serial.Text.Should().Be("2.75\n2.25\n0.63\n10.0\n5.0\n7.5\nD",
            "the register a value is passed in cannot change what the arithmetic answers");
    }

    [Test]
    public void AFloatOperand_IsNotShuffledThroughTheFirstArgumentRegisters()
    {
        var asm = File.ReadAllText(Path.Combine(
            PymcuCompiler.FixtureDir("float-arg-no-shuffle"), "dist", "debug", "firmware.asm"));

        // The shuffle's signature is MOV R18, R22 immediately followed by MOV R19, R23:
        // the second operand being copied from the first argument's registers down into
        // its own. The fixture has exactly one operation that still needs it, `a * k`,
        // whose uint8 operand has to go through __floatsisf in R22:R25.
        CountOperandShuffles(asm).Should().Be(1,
            "only the integer operand still travels through R22:R25");
    }

    /// <summary>Occurrences of the four-register move that copies a freshly loaded
    /// second operand from R22:R25 down into R18:R21.</summary>
    private static int CountOperandShuffles(string asm)
    {
        var lines = asm.Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith(';'))
            .ToArray();

        int found = 0;
        for (int i = 0; i + 3 < lines.Length; i++)
            if (IsMove(lines[i], 18, 22) && IsMove(lines[i + 1], 19, 23)
                && IsMove(lines[i + 2], 20, 24) && IsMove(lines[i + 3], 21, 25))
                found++;
        return found;
    }

    private static bool IsMove(string line, int dst, int src)
        => System.Text.RegularExpressions.Regex.IsMatch(
            line, $@"^MOV\s+R{dst},\s*R{src}\s*$");
}
