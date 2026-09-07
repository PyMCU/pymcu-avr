"""
The lists of AVR chips must name the same chips, and agree on the numbers.

Support for a chip is not recorded in one place but in several, one per layer:

    pymcu/chips/<chip>.py    ram_size and flash_size, as device_info() declares them
    AvrDevices (backend)     where SRAM begins and whether the core has JMP/CALL
    wasi._CHIPS              BFD emulation and multilib for the WASI link
    _SRAM_BYTES/_FLASH_BYTES the linker script's MEMORY regions
    _RAMSTART                where .data begins, as the linker script sees it

The chip file is the source of truth for the two SIZES: the compiler reads them
from device_info() and carries them to the backend in the .mir, so a linker table
that disagrees with the chip file describes a different part than the one the
codegen compiled for. The backend catalog is not that anchor any more and cannot
be: it no longer holds either size, precisely so that they exist in one place.

A chip added to one list and forgotten in another does not fail loudly. Missing
from wasi._CHIPS, the WASI link refuses it and silently falls back to the native
path. Missing from _SRAM_BYTES, the linker script comes out with no MEMORY regions
at all -- and with them goes the overflow check that exists because a 33 KB image
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

import ast
import json
import pathlib
import subprocess

import pytest

# pymcu-avr is not installed in the toolchain-smoke CI job, which installs only
# pymcu-avr-toolchain and pytest on purpose: its question is whether the toolchain
# WHEEL works standalone on each platform. Any module-level import from pymcu made
# that job fail at COLLECTION, which takes the whole file down including the cases
# that need nothing from pymcu-avr. Guarded the way test_avr_supply_chain.py does.
#
# ALL FOUR go in the guard, not just the one the error happened to name. The first
# attempt guarded only avrgas, because that was the import in the message, and the
# job then failed on `import pymcu.chips` instead: a collection error reports the
# first bad import, not the set of them.
try:
    import pymcu.chips
    from pymcu.backend.avr import AvrBackendPlugin
    from pymcu.toolchain.avr import wasi
    from pymcu.toolchain.avr.avrgas import AvrgasToolchain
    from rich.console import Console
    _HAS_AVRGAS = True
except ImportError:
    pymcu = None  # type: ignore[assignment]
    AvrBackendPlugin = None  # type: ignore[assignment]
    wasi = None  # type: ignore[assignment]
    AvrgasToolchain = None  # type: ignore[assignment]
    Console = None  # type: ignore[assignment]
    _HAS_AVRGAS = False

pytestmark = pytest.mark.skipif(
    not _HAS_AVRGAS, reason="pymcu-avr not installed (toolchain-smoke job has only the wheel)")


# Guarding the import is not enough: this line RUNS at import time, so with
# AvrBackendPlugin set to None it raised AttributeError during collection and took
# the file down exactly as the unguarded import had. A guarded name still needs its
# module-level USES guarded, which is a second place to look and not the same place.
if _HAS_AVRGAS:
    BINARY = AvrBackendPlugin.get_backend_binary()
    _binary_missing = not BINARY.exists()
    _binary_reason = f"AVR backend binary not present at {BINARY}"
else:
    BINARY = None
    _binary_missing = True
    _binary_reason = "pymcu-avr not installed"

needs_binary = pytest.mark.skipif(_binary_missing, reason=_binary_reason)


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


def declared_geometry() -> dict[str, dict[str, int]]:
    """{chip: {"ram_size": n, "flash_size": n}} as the chip files declare it.

    Parsed, not imported: a chip file is compiler input full of ptr(0x23) register
    definitions, and executing it under CPython would prove nothing about what the
    compiler reads. The two forms the compiler accepts are a literal and a
    module-level integer constant, so both are resolved here the same way.
    """
    out: dict[str, dict[str, int]] = {}
    for path in sorted(pathlib.Path(pymcu.chips.__file__).parent.glob("*.py")):
        if path.stem == "__init__":
            continue
        tree = ast.parse(path.read_text())

        constants = {
            t.id: node.value.value
            for node in tree.body
            if isinstance(node, ast.Assign) and isinstance(node.value, ast.Constant)
            and isinstance(node.value.value, int)
            for t in node.targets
            if isinstance(t, ast.Name)
        }

        for node in ast.walk(tree):
            if not (isinstance(node, ast.Call) and isinstance(node.func, ast.Name)
                    and node.func.id == "device_info"):
                continue
            kw = {k.arg: k.value for k in node.keywords}
            if not (isinstance(kw.get("arch"), ast.Constant) and kw["arch"].value == "avr"):
                continue

            sizes = {}
            for field in ("ram_size", "flash_size", "eeprom_size"):
                value = kw.get(field)
                if isinstance(value, ast.Constant) and isinstance(value.value, int):
                    sizes[field] = value.value
                elif isinstance(value, ast.Name) and value.id in constants:
                    sizes[field] = constants[value.id]
            out[path.stem] = sizes
    return out


# Same reason as the BINARY block above: these three run at import time and reach
# into names that are None when pymcu-avr is absent.
if _HAS_AVRGAS:
    BACKEND = backend_catalog() if BINARY.exists() else {}
    DECLARED = declared_geometry()
    SRAM = AvrgasToolchain._SRAM_BYTES
    FLASH = AvrgasToolchain._FLASH_BYTES
    WASI_CHIPS = wasi._CHIPS
else:
    BACKEND = {}
    DECLARED = {}
    SRAM = {}
    FLASH = {}
    WASI_CHIPS = {}


def chips() -> list[str]:
    return sorted(BACKEND)


@needs_binary
def test_the_backend_publishes_a_catalog():
    assert len(BACKEND) >= 15, f"the backend published only {len(BACKEND)} chips"


@needs_binary
@pytest.mark.parametrize("name,table", [("wasi._CHIPS", WASI_CHIPS),
                                        ("_SRAM_BYTES", SRAM),
                                        ("_FLASH_BYTES", FLASH)])
def test_the_toolchain_tables_name_the_same_chips_as_the_backend(name, table):
    missing = sorted(set(BACKEND) - set(table))
    extra = sorted(set(table) - set(BACKEND))

    assert not missing, f"{name} has no entry for: {missing}"
    assert not extra, f"{name} names chips the backend does not know: {extra}"


@needs_binary
@pytest.mark.parametrize("chip", chips())
def test_every_chip_the_backend_accepts_has_a_chip_file_declaring_both_sizes(chip):
    """Without the declaration there is nothing for the .mir to carry, and the build
    stops at the first flash table or stack-pointer setup. Better to say so here."""
    sizes = DECLARED.get(chip)
    assert sizes is not None, f"no AVR chip file for {chip} in pymcu/chips/"
    assert "ram_size" in sizes, f"{chip}.py declares no ram_size in its device_info()"
    assert "flash_size" in sizes, f"{chip}.py declares no flash_size in its device_info()"


@needs_binary
@pytest.mark.parametrize("chip", chips())
def test_the_linker_sram_matches_the_declared_ram_size(chip):
    ours = SRAM.get(chip)
    assert ours is not None, f"_SRAM_BYTES has no entry for {chip}"
    assert ours == DECLARED[chip]["ram_size"], \
        (f"{chip}: the linker script sizes SRAM at {ours} bytes, "
         f"{chip}.py declares {DECLARED[chip]['ram_size']}")


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
def test_the_linker_flash_matches_the_declared_flash_size(chip):
    ours = FLASH.get(chip)
    assert ours is not None, f"_FLASH_BYTES has no entry for {chip}"
    assert ours == DECLARED[chip]["flash_size"], \
        (f"{chip}: the linker script sizes flash at {ours} bytes, "
         f"{chip}.py declares {DECLARED[chip]['flash_size']}")
