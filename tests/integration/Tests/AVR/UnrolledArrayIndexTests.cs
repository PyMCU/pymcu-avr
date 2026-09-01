using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// Indexing an unannotated comprehension with a run-time value (PyMCU#64).
///
/// The old message ("Array subscript must be a compile-time constant") blamed the subscript,
/// which is not the problem: the same subscript on an annotated array compiles. Following it
/// leads to trying to make the INDEX constant, which defeats the buffer. The message names
/// the array and the annotation that makes it indexable.
/// </summary>
[TestFixture]
public class UnrolledArrayIndexTests
{
    private const string RuntimeIndexOfComprehension =
        "from pymcu.hal.console import print\n" +
        "from pymcu.hal.gpio import Pin\n" +
        "from pymcu.types import uint8\n" +
        "\n" +
        "\n" +
        "def main():\n" +
        "    p = Pin(\"PB0\", Pin.IN)\n" +
        "    n: uint8 = p.value()\n" +
        "    xs = [i * 2 for i in range(4)]\n" +
        "    print(xs[n])\n";

    private const string Annotated =
        "from pymcu.hal.console import print\n" +
        "from pymcu.hal.gpio import Pin\n" +
        "from pymcu.types import uint8\n" +
        "\n" +
        "\n" +
        "def main():\n" +
        "    p = Pin(\"PB0\", Pin.IN)\n" +
        "    n: uint8 = p.value()\n" +
        "    xs: uint8[4] = [i * 2 for i in range(4)]\n" +
        "    print(xs[n])\n";

    [Test]
    public void UnannotatedComprehension_NamesTheArrayAndTheAnnotation()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => PymcuCompiler.BuildSource(RuntimeIndexOfComprehension));

        // The wording changed with PyMCU da47289d (the #246 fix): the message now explains
        // WHY it cannot be indexed at run time rather than only stating the array has no type.
        // What this test pins has not changed: the message must name the array and show the
        // annotation that fixes it.
        ex!.Message.Should().Contain("'xs' is not addressable at run time here");
        ex.Message.Should().Contain("it lives as separate variables", "the message must say why, not just that");
        ex.Message.Should().Contain("xs: uint8[4]", "the message must show the annotation that fixes it");
        ex.Message.Should().NotContain("subscript must be", "the subscript was never the problem");
    }

    [Test]
    public void AnnotatedComprehension_StillCompiles()
    {
        PymcuCompiler.BuildSource(Annotated).Should().NotBeEmpty(
            "the annotated spelling is the one the message points at, so it must work");
    }
}
