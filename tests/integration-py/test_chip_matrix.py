"""
Every chip in the catalog must assemble, link and boot with its own stack pointer.

Two bugs hid behind a corpus that only ever built for the ATmega328P. The stack
pointer was initialised to the 328P's RAMEND (0x08FF) on every ATmega, so an
ATmega48 pushed 1.5 KB past the end of its 512-byte SRAM; and the ATmega48/88
parts (avr4) and every ATtiny (avr25) have no JMP/CALL, so any program with a
real function call was rejected by the assembler -- "illegal opcode ... for mcu
atmega48", naming an opcode one entry off in avr-as's own table.

The programs below are deliberately HAL-free (no Pin, no UART): what is under
test is the chip's memory layout and instruction set, not its peripheral
coverage.

Run with:
    pytest tests/integration-py/test_chip_matrix.py -v
"""

from __future__ import annotations

import re

import avr8sharp
import pytest

import pymcu_compiler

CALLS_A_FUNCTION = """\
from pymcu.types import uint8


def step(n: uint8) -> uint8:
    return n + 1


def main():
    x: uint8 = 0
    while True:
        x = step(x)
"""

# No call, so the stack stays exactly where the startup code put it.
SPINS = """\
from pymcu.types import uint8


def main():
    x: uint8 = 0
    while True:
        x = x + 1
"""

# The SRAM check reserves a flat 64 bytes for the call stack, which is the whole
# SRAM of an ATtiny13, so no program with static data has ever compiled for it.
NO_ROOM_FOR_STATICS = {"attiny13", "attiny13a"}


def catalog() -> list[tuple[str, int]]:
    """(chip, RAMEND) for every AVR chip PyMCU ships, read from its chip file."""
    chips_dir = pymcu_compiler.REPO_ROOT.parent / "pymcu" / "lib" / "src" / "pymcu" / "chips"
    assert chips_dir.is_dir(), f"chip catalog not found at {chips_dir}"
    out = []
    for path in sorted(chips_dir.glob("*.py")):
        text = path.read_text()
        if not re.search(r'device_info\([^)]*arch\s*=\s*"avr"', text):
            continue
        start = re.search(r"^RAM_START\s*=\s*(0x[0-9A-Fa-f]+|\d+)", text, re.M)
        size = re.search(r"^RAM_SIZE\s*=\s*(0x[0-9A-Fa-f]+|\d+)", text, re.M)
        assert start and size, f"{path.name} declares no RAM_START/RAM_SIZE"
        out.append((path.stem, int(start.group(1), 0) + int(size.group(1), 0) - 1))
    assert out, "the AVR chip catalog came back empty"
    return out


def cases() -> list:
    return [
        pytest.param(chip, ramend, id=chip,
                     marks=pytest.mark.xfail(strict=True, reason="64-byte call-stack floor")
                     if chip in NO_ROOM_FOR_STATICS else ())
        for chip, ramend in catalog()
    ]


@pytest.mark.parametrize("chip,ramend", cases())
def test_a_program_with_a_call_assembles(chip, ramend):
    assert pymcu_compiler.build_source(CALLS_A_FUNCTION, target=chip, frequency=8_000_000)


@pytest.mark.parametrize("chip,ramend", cases())
def test_stack_pointer_is_the_chips_own_ramend(chip, ramend):
    firmware = pymcu_compiler.build_source(SPINS, target=chip, frequency=8_000_000)
    try:
        sim = avr8sharp.board(chip)
    except Exception:
        pytest.skip(f"avr8sharp has no simulation preset for {chip}")

    try:
        sim.with_hex(firmware)
        sim.run_ms(1)
        assert sim.cpu.sp == ramend
    finally:
        sim.close()
