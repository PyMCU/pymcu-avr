// SPDX-License-Identifier: MIT
using FluentAssertions;
using NUnit.Framework;

namespace PyMCU.IntegrationTests.Tests.AVR;

/// <summary>
/// fixtures/compat-cp-framebuf-text: unmodified upstream <c>adafruit_framebuf.py</c>,
/// <c>FrameBuffer.text("PyMCU", 0, 0, 1)</c>.
///
/// This is the CircuitPython display idiom end to end: <c>text()</c> splits the string
/// on <c>"\n"</c>, enumerates the chunk, and calls <c>BitmapFont(font_name)</c> --
/// which opens the font file. A PyMCU program has no filesystem, so compilation must
/// stop at <c>open(self.font_name, "rb")</c> with a diagnostic that names the resolved
/// filename (<c>font5x8.bin</c>) and points at RFC 0008 (embedded files). Getting there
/// at all is the finding: <c>split</c>, <c>enumerate</c>, the <c>self._font is None</c>
/// guard, and the kwarg call all have to compile first.
/// </summary>
[TestFixture]
public class CompatCpFramebufTextTests
{
    [Test]
    public void Text_ReachesOpen_AndNamesTheFileAndRfc()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => PymcuCompiler.BuildFixture("compat-cp-framebuf-text"));

        ex!.Message.Should().Contain("font5x8.bin",
            because: "the diagnostic names the font file the program resolved");
        ex.Message.Should().Contain("RFC 0008",
            because: "embedded files are RFC 0008 work -- the diagnostic says so");
        ex.Message.Should().Contain("adafruit_framebuf.py",
            because: "the refusal is inside the vendored upstream file, not main.py");
    }

    [Test]
    public void Text_SameDiagnosticThroughThePythonFrontEnd()
    {
        var ex = Assert.Throws<InvalidOperationException>(
            () => PymcuCompiler.BuildFixturePyParser("compat-cp-framebuf-text"));

        ex!.Message.Should().Contain("font5x8.bin");
        ex.Message.Should().Contain("RFC 0008");
    }
}
