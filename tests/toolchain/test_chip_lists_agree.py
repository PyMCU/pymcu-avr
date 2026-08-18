"""
The four lists of AVR chips must name the same chips, and agree on the numbers.

Support for a chip is not recorded in one place but in four, one per layer:

    AvrDevices (backend)     SRAM layout and whether the core has JMP/CALL
    wasi._CHIPS              BFD emulation and multilib for the WASI link
    _SRAM_BYTES/_FLASH_BYTES the linker script's MEMORY regions
    _RAMSTART                where .data begins, as the linker script sees it

A chip added to one and forgotten in another does not fail loudly. Missing from
wasi._CHIPS, the WASI link refuses it and silently falls back to the native path.
Missing from _SRAM_BYTES, the linker script comes out with no MEMORY regions at
all -- and with them goes the overflow check that exists because a 33 KB image
once linked "successfully" for a 32 KB part. A _RAMSTART that disagrees with the
backend's is worse still: the codegen addresses its frame from one base and the
linker places .data at another, which is how the ATmega2560 put every static in
extended I/O.

The backend's list is read by asking the binary (`pymcuc-avr devices`), never by
parsing its source: what is under test is what the compiler uses, not what its
source looks like.

Run with:
    pytest tests/toolchain/test_chip_lists_agree.py -v
"""

from __future__ import annotations

import json
import subprocess

import pytest
from rich.console import Console

from pymcu.backend.avr import AvrBackendPlugin
from pymcu.toolchain.avr import wasi
from pymcu.toolchain.avr.avrgas import AvrgasToolchain

BINARY = AvrBackendPlugin.get_backend_binary()

needs_binary = pytest.mark.skipif(
    not BINARY.exists(), reason=f"AVR backend binary not present at {BINARY}")


def backend_catalog() -> dict[str, dict]:
    """{chip: row} as published by the backend itself.

    A binary that is present but cannot answer is a broken instrument, never a
    skip: it fails here and says which of the three ways it failed.
    """
    proc = subprocess.run([str(BINARY), "devices"], capture_output=True, text=True)
    assert proc.returncode == 0, \
        f"`{BINARY.name} devices` exited {proc.returncode}: {proc.stderr.strip()[:200]}"
    try:
        rows = json.loads(proc.stdout)
    except json.JSONDecodeError as exc:
        raise AssertionError(
            f"`{BINARY.name} devices` did not answer with JSON: {exc}\n"
            f"{proc.stdout[:200]}") from exc
    assert rows, f"`{BINARY.name} devices` answered with an empty catalog"
    return {r["Chip"]: r for r in rows}


BACKEND = backend_catalog() if BINARY.exists() else {}
SRAM = AvrgasToolchain._SRAM_BYTES
FLASH = AvrgasToolchain._FLASH_BYTES


def chips() -> list[str]:
    return sorted(BACKEND)


@needs_binary
def test_the_backend_publishes_a_catalog():
    assert len(BACKEND) >= 15, f"the backend published only {len(BACKEND)} chips"


@needs_binary
@pytest.mark.parametrize("name,table", [("wasi._CHIPS", wasi._CHIPS),
                                        ("_SRAM_BYTES", SRAM),
                                        ("_FLASH_BYTES", FLASH)])
def test_the_toolchain_tables_name_the_same_chips_as_the_backend(name, table):
    missing = sorted(set(BACKEND) - set(table))
    extra = sorted(set(table) - set(BACKEND))

    assert not missing, f"{name} has no entry for: {missing}"
    assert not extra, f"{name} names chips the backend does not know: {extra}"


@needs_binary
@pytest.mark.parametrize("chip", chips())
def test_the_linker_sram_matches_the_backend_ram_size(chip):
    ours = SRAM.get(chip)
    assert ours is not None, f"_SRAM_BYTES has no entry for {chip}"
    assert ours == BACKEND[chip]["RamSize"], \
        (f"{chip}: the linker script sizes SRAM at {ours} bytes, "
         f"the backend at {BACKEND[chip]['RamSize']}")


@needs_binary
@pytest.mark.parametrize("chip", chips())
def test_the_linker_data_base_matches_the_backend_ram_start(chip):
    """The ATmega2560 case: .data placed at one base, the frame addressed from another."""
    linker = AvrgasToolchain(Console(), chip=chip)._chip_ramstart()

    assert linker == BACKEND[chip]["RamStart"], \
        (f"{chip}: the linker script starts .data at 0x{linker:04X}, "
         f"the backend addresses from 0x{BACKEND[chip]['RamStart']:04X}")


@needs_binary
@pytest.mark.parametrize("chip", chips())
def test_the_linker_flash_matches_the_backend_flash_size(chip):
    ours = FLASH.get(chip)
    assert ours is not None, f"_FLASH_BYTES has no entry for {chip}"
    assert ours == BACKEND[chip]["FlashSize"], \
        (f"{chip}: the linker script sizes flash at {ours} bytes, "
         f"the backend at {BACKEND[chip]['FlashSize']}")
