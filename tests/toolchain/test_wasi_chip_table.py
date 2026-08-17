"""
Regression tests for the WASI backend's chip table.

Every value in it was read out of `avr-gcc -mmcu=<chip> -###` and
`--print-libgcc-file-name`, never derived from the chip's family name, because
the family name gets two cases wrong in ways that produce working-looking but
incorrect firmware:

  * attiny13 and attiny85 are both avr25 to the linker, yet attiny13 must link
    against the avr25/tiny-stack libgcc -- the 8-bit-stack-pointer build. The
    plain avr25 one writes SPH, a register attiny13 does not have.
  * the ATmega48/88 parts are avr4 while the 168/328 parts are avr5, which the
    numbering does not hint at either.

The table also has to stay inside what the wheel ships: the toolchain build
prunes multilibs to KEEP_MULTILIBS="avr25 avr4 avr5 avr6", so a chip on another
core needs that multilib restored in avr-gcc-build too.

Run with:
    pytest tests/toolchain/test_wasi_chip_table.py -v
"""
from __future__ import annotations

from pathlib import Path

import pytest

from pymcu.toolchain.avr import wasi


@pytest.mark.parametrize("chip,emulation", [
    ("atmega328p", "avr5"),
    ("atmega168p", "avr5"),
    ("atmega32u4", "avr5"),
    ("atmega88", "avr4"),
    ("atmega48p", "avr4"),
    ("atmega2560", "avr6"),
    ("attiny85", "avr25"),
    ("attiny13", "avr25"),
])
def test_emulation(chip, emulation):
    assert wasi.emulation_for(chip) == emulation


@pytest.mark.parametrize("chip,libdir", [
    ("atmega328p", "avr5"),
    ("atmega2560", "avr6"),
    ("attiny13", "avr25/tiny-stack"),
    ("attiny13a", "avr25/tiny-stack"),
    ("attiny24", "avr25/tiny-stack"),
    ("attiny25", "avr25/tiny-stack"),
    ("attiny2313", "avr25/tiny-stack"),
    ("attiny44", "avr25"),
    ("attiny85", "avr25"),
    ("attiny4313", "avr25"),
])
def test_library_directory(chip, libdir):
    assert wasi.multilib_for(chip) == libdir


def test_tiny_stack_parts_do_not_share_libraries_with_plain_avr25():
    """The whole point of splitting emulation from library directory."""
    assert wasi.emulation_for("attiny13") == wasi.emulation_for("attiny85")
    assert wasi.multilib_for("attiny13") != wasi.multilib_for("attiny85")


def test_table_matches_pymcus_chip_list():
    """The table is PyMCU's chip list, no more and no less. A chip PyMCU cannot
    target has no business here, and one it can target must not be missing."""
    chips_dir = (Path.home() / "Repos" / "PyMCU" / "lib" / "src" / "pymcu" / "chips")
    if not chips_dir.is_dir():
        pytest.skip("PyMCU checkout not available")
    supported = {f.stem for f in chips_dir.glob("*.py")
                 if f.stem.startswith(("atmega", "attiny"))}
    assert {c for c in wasi._CHIPS} == supported


@pytest.mark.parametrize("libdir", ["avr4", "avr5", "avr6", "avr25", "avr25/tiny-stack"])
def test_every_chip_stays_inside_the_shipped_multilibs(libdir):
    """KEEP_MULTILIBS in avr-gcc-build prunes everything else, so no chip in the
    table may need a directory the wheel no longer carries."""
    kept = {"avr4", "avr5", "avr6", "avr25", "avr25/tiny-stack"}
    assert libdir in kept
    assert {d for _, d in wasi._CHIPS.values()} <= kept


def test_unknown_chip_refuses_instead_of_guessing(monkeypatch):
    tools = object.__new__(wasi.WasiAvrTools)
    with pytest.raises(wasi.WasiUnavailable, match="not in the verified chip table"):
        wasi.WasiAvrPipeline(tools, "atmega644pa", 0x800100)


def test_sysroot_is_keyed_by_library_directory(tmp_path):
    root = tmp_path / "pkg" / "wasm"
    root.mkdir(parents=True)
    for libdir in ("avr25", "avr25/tiny-stack"):
        d = root.parent / "sysroot" / libdir
        d.mkdir(parents=True)
        (d / "libgcc.a").write_bytes(b"")
        (d / "libm.a").write_bytes(b"")

    plain = wasi.sysroot_dir(root, wasi.multilib_for("attiny85"))
    tiny = wasi.sysroot_dir(root, wasi.multilib_for("attiny13"))
    assert plain is not None and tiny is not None
    assert plain != tiny
    assert tiny.name == "tiny-stack"


# ---------------------------------------------------------------------------
# C/C++ routing: the [ffi] extra decides who compiles
# ---------------------------------------------------------------------------

def test_compile_c_without_a_front_end_falls_back(monkeypatch, tmp_path):
    """An install without cc1/cc1plus must still use the native avr-gcc, not fail."""
    from rich.console import Console

    from pymcu.toolchain.avr.avrgas import AvrgasToolchain

    tc = AvrgasToolchain(Console(), chip="atmega328p")
    pipeline = wasi.WasiAvrPipeline(object.__new__(wasi.WasiAvrTools),
                                    "atmega328p", 0x800100, ffi_factory=None)
    monkeypatch.setattr(tc, "_wasi_pipeline", lambda: pipeline)

    called = {}

    def fake_find(name):
        called["name"] = name
        raise RuntimeError("stop here")

    monkeypatch.setattr(tc, "_find_bin_for_ffi", fake_find)
    with pytest.raises(RuntimeError, match="stop here"):
        tc.compile_c([tmp_path / "x.c"], [], [], tmp_path)
    assert called["name"] == "avr-gcc"


def test_pipeline_without_ffi_refuses_to_compile():
    pipeline = wasi.WasiAvrPipeline(object.__new__(wasi.WasiAvrTools),
                                    "atmega328p", 0x800100, ffi_factory=None)
    with pytest.raises(wasi.WasiUnavailable, match="cc1/cc1plus"):
        pipeline.compile_c([], [], [], Path("."))
