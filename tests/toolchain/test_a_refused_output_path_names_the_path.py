"""A filesystem refusal of the output path is not a codegen failure.

`pymcuc-avr` wrapped everything after it had parsed its arguments in one catch that
printed

    [pymcuc-avr] Codegen failed: <.NET's words>

so a directory that could not be written, a disk that was full, or two builds handed the
same output path all came out labelled as a failure of the backend. Nothing in the backend
failed in any of those cases, and the label sends the reader into codegen to look for a
bug that is not there. Whether the path is named at all is left to .NET, and no message
ever says that the path was the OUTPUT.

PyMCU#498 is what that costs. On the pymcuc side of the same chain, 68 call sites across
two suites passed `/dev/null` as an output; two suites at once made them race, and the
loser reported a crash that read as a miscompilation of whatever it was building. The
compiler's half is fixed; this is the backend's.

The minimal `.mir` here is deliberate. It carries no device geometry, so codegen refuses
it -- but only AFTER the output file has been opened, which is exactly the window under
test. That also gives the third case its meaning: with a writable path, the same input
must still produce the codegen error, unrelabelled, because a guard that swallowed real
failures would pass the first two cases just as well.
"""

import subprocess

import pytest

try:
    from pymcu.backend.avr import AvrBackendPlugin
except Exception:  # pragma: no cover - the plugin is what this suite tests
    AvrBackendPlugin = None  # type: ignore[assignment]

if AvrBackendPlugin is not None:
    BINARY = AvrBackendPlugin.get_backend_binary()
    _missing = not BINARY.exists()
    _reason = f"AVR backend binary not present at {BINARY}"
else:
    BINARY = None
    _missing = True
    _reason = "pymcu-avr not installed"

needs_binary = pytest.mark.skipif(_missing, reason=_reason)

# Enough of a program for the backend to get as far as opening its output.
MINIMAL_MIR = '{"functions":[]}'


def run(tmp_path, output):
    source = tmp_path / "in.mir"
    source.write_text(MINIMAL_MIR)
    proc = subprocess.run(
        [str(BINARY), str(source), "-o", str(output), "--target", "atmega328p"],
        capture_output=True, text=True,
    )
    return proc.returncode, proc.stdout + proc.stderr


@needs_binary
@pytest.mark.parametrize("kind", ["the-path-is-a-directory", "the-parent-is-a-file"])
def test_a_refused_output_path_is_named_and_is_not_blamed_on_codegen(tmp_path, kind):
    if kind == "the-path-is-a-directory":
        output = tmp_path / "adir"
        output.mkdir()
    else:
        # A regular file where a directory has to go.
        (tmp_path / "afile").write_text("")
        output = tmp_path / "afile" / "out.asm"

    code, out = run(tmp_path, output)

    assert code == 1, out
    # Separately, so a regression says which claim broke.
    assert "Codegen failed" not in out, out
    assert "cannot write the output file" in out, out
    assert str(output) in out, out


@needs_binary
def test_a_codegen_failure_is_still_reported_as_one(tmp_path):
    """The anchor. A guard drawn too wide would relabel this one too."""
    code, out = run(tmp_path, tmp_path / "out.asm")

    assert code == 1, out
    assert "cannot write the output file" not in out, out
    assert "Codegen failed" in out, out
