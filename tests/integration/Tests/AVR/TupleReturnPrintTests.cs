using System;
using System.Linq;
using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/tuple-return-print: `print(r)` and `print(make())` over a `-> tuple`
/// outlined function write the tuple text, not the object pointer's low byte.
///
/// Two backend/ordering defects hid it: `ret` marshaled by the DECLARED type, which
/// stays UNKNOWN for `-> tuple` (one byte — the pointer's high half never left the
/// callee); and the element type never reached a module-level call site because
/// `main` lowers before outlined bodies do. The fixture also pins `sind fconst`:
/// a float literal stored into a float field emitted no materialization at all and
/// the read-back came back as whatever the registers held.
/// </summary>
[TestFixture]
public class TupleReturnPrintTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("tuple-return-print"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("tuple-return-print"));
    }

    private static string[] Lines(SimSession s)
    {
        var uno = s.Reset();
        uno.RunMilliseconds(400);
        return uno.Serial.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                              .Select(l => l.Trim()).ToArray();
    }

    private static readonly string[] Cpython =
    {
        "named: (11, 222)",
        "direct: (11, 222)",
        "field: 1.5",
        "END",
    };

    [Test]
    public void NamedAndDirectCallsPrintTheTuple()
        => Lines(_session).Should().Equal(Cpython);

    [Test]
    public void TheSameThroughThePythonFrontEnd()
        => Lines(_pySession).Should().Equal(Cpython);
}
