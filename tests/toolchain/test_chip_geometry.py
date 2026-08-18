"""
Regression tests for the per-chip geometry the toolchain hands to avr-as and avr-ld.

Every expectation below is what avr-gcc itself reports for the chip -- RAMSTART,
RAMEND and __AVR_HAVE_JMP_CALL__ from `avr-gcc -mmcu=<chip> -dM -E` -- never a
value derived from the chip's family name, because the family name gets the
ATmega48/88 parts wrong: they are avr4 and have no JMP/CALL, while the 168/328
parts of the same family are avr5 and do. Rewriting their RJMP/RCALL into
JMP/CALL made avr-as reject the firmware outright ("illegal opcode ... for mcu
atmega48"; the opcode avr-as names is off by one entry in its own table, so the
line it points at is a JMP when it says asr, and a CALL when it says jmp).

Run with:
    pytest tests/toolchain/test_chip_geometry.py -v
"""
from __future__ import annotations

import re

import pytest
from rich.console import Console

from pymcu.toolchain.avr.avrgas import AvrgasToolchain

# chip -> (RAMSTART, SRAM bytes, has JMP/CALL, flash bytes = FLASHEND + 1)
CHIPS = {
    "atmega48":   (0x100, 512,  False, 4096),
    "atmega48p":  (0x100, 512,  False, 4096),
    "atmega88":   (0x100, 1024, False, 8192),
    "atmega88p":  (0x100, 1024, False, 8192),
    "atmega168":  (0x100, 1024, True, 16384),
    "atmega168p": (0x100, 1024, True, 16384),
    "atmega328":  (0x100, 2048, True, 32768),
    "atmega328p": (0x100, 2048, True, 32768),
    "atmega32u4": (0x100, 2560, True, 32768),
    "atmega2560": (0x200, 8192, True, 262144),
    "attiny13":   (0x60,  64,   False, 1024),
    "attiny13a":  (0x60,  64,   False, 1024),
    "attiny24":   (0x60,  128,  False, 2048),
    "attiny25":   (0x60,  128,  False, 2048),
    "attiny2313": (0x60,  128,  False, 2048),
    "attiny44":   (0x60,  256,  False, 4096),
    "attiny45":   (0x60,  256,  False, 4096),
    "attiny4313": (0x60,  256,  False, 4096),
    "attiny84":   (0x60,  512,  False, 8192),
    "attiny85":   (0x60,  512,  False, 8192),
}

RELATIVE_CALL = "\tRCALL\thelper\n\tRJMP\tmain\n"


def toolchain(chip: str) -> AvrgasToolchain:
    return AvrgasToolchain(Console(), chip=chip)


@pytest.mark.parametrize("chip,has_jmp", [(c, v[2]) for c, v in CHIPS.items()])
def test_jmp_call_support_per_chip(chip, has_jmp):
    assert toolchain(chip)._has_jmp() is has_jmp


@pytest.mark.parametrize("chip", [c for c, v in CHIPS.items() if not v[2]])
def test_relative_forms_survive_translation(chip):
    tc = toolchain(chip)

    out = tc._preprocess_asm(RELATIVE_CALL, has_jmp=tc._has_jmp())

    assert "RCALL" in out and "RJMP" in out
    assert not re.search(r"^\s*(JMP|CALL)\b", out, re.MULTILINE)


@pytest.mark.parametrize("chip", [c for c, v in CHIPS.items() if v[2]])
def test_absolute_forms_are_used_where_they_exist(chip):
    tc = toolchain(chip)

    out = tc._preprocess_asm(RELATIVE_CALL, has_jmp=tc._has_jmp())

    assert "\tCALL\thelper" in out and "\tJMP\tmain" in out


@pytest.mark.parametrize("chip,ramstart,sram", [(c, v[0], v[1]) for c, v in CHIPS.items()])
def test_linker_script_describes_the_real_sram(chip, ramstart, sram):
    script = toolchain(chip)._default_ld_script()

    region = re.search(r"sram\s*\(rw!x\)\s*:\s*ORIGIN\s*=\s*(0x[0-9A-Fa-f]+),"
                       r"\s*LENGTH\s*=\s*(-?\d+)", script)
    assert region, f"no sram MEMORY region in the linker script for {chip}"
    assert int(region.group(1), 16) == 0x800000 + ramstart
    assert int(region.group(2)) == sram


@pytest.mark.parametrize("chip,flash", [(c, v[3]) for c, v in CHIPS.items()])
def test_the_linker_knows_the_flash_size(chip, flash):
    assert AvrgasToolchain._FLASH_BYTES[chip] == flash
