using System;
using System.Linq;
using Avr8Sharp.TestKit;
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/module-bytearray-local-collision (PyMCU#458): a module-level container global must
/// not leak into a linked stdlib function's locals by bare name.
///
/// `data` collides with the `data: uint8` local in `uart_rx_read` (return-value check) and `b`
/// collides with the `b: uint8` parameter in `uart_write_byte_repr` (comparison operand check).
/// Both functions link through the UART module that `print` pulls in, so the unfixed compiler
/// refuses this program inside `hal/avr/uart/avr.py` with "a bytes or list object cannot be
/// returned/compared" — a limitation the stdlib function does not have. The global keeps its
/// container type in user code and the local keeps its scalar type inside the function; the
/// checks must see the local.
/// </summary>
[TestFixture]
public class ModuleBytearrayLocalCollisionTests
{
    private static SimSession _session = null!;
    private static SimSession _pySession = null!;

    [OneTimeSetUp]
    public void BuildFirmware()
    {
        _session   = new SimSession(PymcuCompiler.BuildFixture("module-bytearray-local-collision"));
        _pySession = new SimSession(PymcuCompiler.BuildFixturePyParser("module-bytearray-local-collision"));
    }

    private static string[] Lines(SimSession s)
    {
        var uno = s.Reset();
        uno.RunUntilSerial(uno.Serial, "done\n", maxMs: 3000);
        return uno.Serial.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                              .Select(l => l.Trim()).ToArray();
    }

    private static readonly string[] Cpython = { "7", "42", "4", "done" };

    [Test]
    public void TheGlobalArray_ReadsBackItsOwnBytes()
    {
        Lines(_session).Should().Equal(Cpython,
            "the user's `data`/`b` globals must survive the HAL functions that share their " +
            "names; a local scalar still shadows them, and `global data` still reaches them");
    }

    [Test]
    public void TheGlobalArray_ReadsBackItsOwnBytes_PyParser()
    {
        Lines(_pySession).Should().Equal(Cpython,
            "the same collision must not leak under the Python front end either");
    }
}
