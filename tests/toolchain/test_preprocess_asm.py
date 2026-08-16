"""
Regression tests for the assembly translation step.

_preprocess_asm turns AVRA word addresses into GNU AS byte addresses by
multiplying .org operands by two, which cannot be idempotent: a second pass
doubles again and silently stretches the interrupt vector table from 4-byte to
8-byte slots.  Nothing fails -- the file still assembles and still links -- so
the firmware is wrong without a single diagnostic.

The fix is structural: assemble() writes the translation to firmware.gas.asm and
leaves the compiler's own firmware.asm untouched, so the step can never be fed
its own output.  These tests pin both halves: that the transformation really is
non-idempotent (so nobody "fixes" it by running it twice), and that the pipeline
never re-reads what it wrote.

Run with:
    pytest tests/toolchain/test_preprocess_asm.py -v
"""
from __future__ import annotations

import re
from pathlib import Path

import pytest
from rich.console import Console

from pymcu.toolchain.avr.avrgas import AvrgasToolchain

# A vector table the way pymcuc emits it: word-addressed .org, one JMP per slot.
VECTOR_TABLE = """\
.equ RAMSTART = 0x0100
.org 0x0
\tJMP\tmain
.org 0x2
\tJMP\t__bad_interrupt
.org 0x4
\tJMP\t__bad_interrupt
.org 0x6
\tJMP\t__bad_interrupt
main:
\tRJMP\tmain
__bad_interrupt:
\tRJMP\t__bad_interrupt
"""


def orgs(text: str) -> list[int]:
    return [int(v, 0) for v in re.findall(r"^\s*\.org\s+(\S+)", text, re.MULTILINE)]


@pytest.fixture
def toolchain() -> AvrgasToolchain:
    return AvrgasToolchain(Console(), chip="atmega328p")


def test_word_addresses_become_byte_addresses(toolchain):
    once = toolchain._preprocess_asm(VECTOR_TABLE, has_jmp=True)
    assert orgs(once) == [0x0, 0x4, 0x8, 0xC]
    # An AVR JMP is 4 bytes, so consecutive vectors must be 4 bytes apart.
    assert [b - a for a, b in zip(orgs(once), orgs(once)[1:])] == [4, 4, 4]


def test_preprocess_is_not_idempotent(toolchain):
    """Pinned deliberately: doubling cannot be idempotent, so the pipeline must
    never run this step on its own output.  If someone ever makes it idempotent
    this test should be deleted along with the reason it exists."""
    once = toolchain._preprocess_asm(VECTOR_TABLE, has_jmp=True)
    twice = toolchain._preprocess_asm(once, has_jmp=True)
    assert once != twice
    assert orgs(twice) == [0x0, 0x8, 0x10, 0x18]


@pytest.mark.parametrize("source", [
    ".equ FOO = 1\n",
    "\tLDI\tr30, high(main)\n",
    "\tRCALL\tmain\n",
    "\tLDI\tr30, hi8(main * 2)\n",
])
def test_other_translations_are_idempotent(toolchain, source):
    once = toolchain._preprocess_asm(source, has_jmp=True)
    assert toolchain._preprocess_asm(once, has_jmp=True) == once


def test_assemble_does_not_rewrite_the_compiler_output(toolchain, tmp_path, monkeypatch):
    """The whole point of the fix: firmware.asm must survive assemble() intact."""
    asm = tmp_path / "firmware.asm"
    asm.write_text(VECTOR_TABLE)

    monkeypatch.setattr(toolchain, "_wasi_pipeline", lambda: None)
    monkeypatch.setattr(toolchain, "_find_bin", lambda name: "/nonexistent/avr-as")
    with pytest.raises(Exception):
        toolchain.assemble(asm)

    assert asm.read_text() == VECTOR_TABLE
    gas = tmp_path / "firmware.gas.asm"
    assert gas.exists()
    assert orgs(gas.read_text()) == [0x0, 0x4, 0x8, 0xC]


def test_assembling_twice_is_stable(toolchain, tmp_path, monkeypatch):
    """Running the step twice used to double the vector spacing; now it cannot."""
    asm = tmp_path / "firmware.asm"
    asm.write_text(VECTOR_TABLE)
    gas = tmp_path / "firmware.gas.asm"

    monkeypatch.setattr(toolchain, "_wasi_pipeline", lambda: None)
    monkeypatch.setattr(toolchain, "_find_bin", lambda name: "/nonexistent/avr-as")

    for _ in range(3):
        with pytest.raises(Exception):
            toolchain.assemble(asm)
    assert orgs(gas.read_text()) == [0x0, 0x4, 0x8, 0xC]


def test_ffi_without_a_front_end_names_the_package(toolchain, monkeypatch):
    """A project with C sources and no compiler at all must be told which package
    to install, not that some binary is missing."""
    monkeypatch.setattr(toolchain, "_wasi_pipeline", lambda: object())

    def _missing(name: str) -> str:
        raise RuntimeError(f"{name} not found. Run 'pymcu build' to install the AVR toolchain.")

    monkeypatch.setattr(toolchain, "_find_bin", _missing)
    with pytest.raises(RuntimeError, match="pymcu-avr-toolchain-wasi"):
        toolchain._find_bin_for_ffi("avr-gcc")


def test_plain_build_keeps_the_original_message(toolchain, monkeypatch):
    """Without the WASI toolchain there is no extra to point at, so the original
    message is the right one."""
    monkeypatch.setattr(toolchain, "_wasi_pipeline", lambda: None)

    def _missing(name: str) -> str:
        raise RuntimeError(f"{name} not found. Run 'pymcu build' to install the AVR toolchain.")

    monkeypatch.setattr(toolchain, "_find_bin", _missing)
    with pytest.raises(RuntimeError) as excinfo:
        toolchain._find_bin_for_ffi("avr-gcc")
    assert "pymcu-avr-toolchain-wasi" not in str(excinfo.value)
