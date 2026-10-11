"""
Tests for the cc1 preprocessor exposed by the WASI backend.

The vendor headers are the authority for register geometry, so the stdlib checks
ask the same cc1 the compiler uses what a chip's macros expand to. These tests pin
that path: the flags come from cc1_flags.json, the headers come from the wheel, and
the answer is the expanded text rather than a guess from the chip name.

Run with:
    pytest tests/toolchain/test_wasi_preprocess.py -v
"""
from __future__ import annotations

import pytest

try:
    from pymcu.toolchain.avr import wasi
    _HAS_PYMCU_AVR = True
except ImportError:
    wasi = None  # type: ignore[assignment]
    _HAS_PYMCU_AVR = False

pytestmark = pytest.mark.skipif(
    not _HAS_PYMCU_AVR, reason="pymcu-avr not installed (toolchain-smoke job has only the wheel)")

PROBE = "#include <avr/io.h>\nX RAMEND FLASHEND\n"


def _preprocess(chip, extra=None):
    text = wasi.preprocess_for_chip(chip, PROBE, extra)
    if text is None:
        pytest.skip("WASI AVR toolchain not available")
    return text


def _probe_line(text):
    return next(line for line in text.splitlines() if line.startswith("X "))


def test_macros_list_has_the_jmp_call_flag_on_atmega328p():
    macros = _preprocess("atmega328p", ["-dM"])
    assert "__AVR_HAVE_JMP_CALL__" in macros


def test_macros_list_has_no_jmp_call_flag_on_attiny85():
    macros = _preprocess("attiny85", ["-dM"])
    assert "__AVR_HAVE_JMP_CALL__" not in macros


def test_ramend_and_flashend_expand_on_atmega328p():
    text = _preprocess("atmega328p", ["-P"])
    assert _probe_line(text).split()[1:] == ["0x8FF", "0x7FFF"]


def test_unknown_chip_is_none_not_a_guess():
    assert wasi.preprocess_for_chip("no-such-chip", PROBE) is None


def test_missing_toolchain_is_none(monkeypatch):
    monkeypatch.setattr(wasi, "find_ffi_root", lambda: None)
    assert wasi.preprocess_for_chip("atmega328p", PROBE) is None
