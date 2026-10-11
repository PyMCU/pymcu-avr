"""
AVR toolchain supply-chain tests for the WASI toolchain package.

The AVR toolchain ships as pymcu-avr-toolchain-wasi: avr-as, avr-ld, avr-objcopy, cc1 and
cc1plus as wasm32-wasip1 modules, the libgcc and libm sysroots, the headers, and the flags
table that makes cc1 produce what avr-gcc would for each chip. The driver runs that package,
not the native avr-gcc wheel this file used to test. The native checks (the avr-gcc driver's
device specs, libexec permissions, the bin/as and bin/ld symlinks) have no counterpart in
WASI, which has no gcc driver to validate, so they were removed rather than ported.

Decision record: the package is checked for completeness (every tool module, a flags row and
a sysroot for every chip the driver targets) and the driver's pipeline is checked end to end
(C and assembly to a valid HEX). Both halves run on every platform where the package is
installed, and the pipeline half also needs wasmtime and pymcu-avr; without them it skips.

Run with:
    pytest tests/toolchain/test_avr_supply_chain.py -v
"""
from __future__ import annotations

import json
from pathlib import Path

import pytest

# ---------------------------------------------------------------------------
# Toolchain package guard
# ---------------------------------------------------------------------------

try:
    import pymcu_avr_toolchain_wasi as _pkg
    _HAS_PACKAGE = True
    _PACKAGE_ERR = ""
except ImportError as exc:
    _pkg = None  # type: ignore[assignment]
    _HAS_PACKAGE = False
    _PACKAGE_ERR = str(exc)

pytestmark = pytest.mark.skipif(
    not _HAS_PACKAGE,
    reason=f"pymcu-avr-toolchain-wasi not installed: {_PACKAGE_ERR}",
)

_TOOLS = ("avr-as", "avr-ld", "avr-objcopy", "cc1", "cc1plus")

# The library subdirectories the chip table can name (see wasi._CHIPS in pymcu-avr).
_MULTILIBS = ("avr4", "avr5", "avr6", "avr25", "avr25/tiny-stack")

# The AVR chips PyMCU targets: 20 of them, each needs a flags row in the table.
_EXPECTED_CHIPS = 20

_MINIMAL_C = "int add(int a, int b) { return a + b; }"

_MINIMAL_ASM = """\
.global main
main:
    nop
    ret
"""


def _flags_table() -> dict:
    return json.loads(_pkg.FLAGS_TABLE.read_text())


# ---------------------------------------------------------------------------
# 1. Package completeness
# ---------------------------------------------------------------------------


class TestPackage:
    """What the wheel must contain for the driver to run without a native toolchain."""

    @pytest.mark.parametrize("tool", _TOOLS)
    def test_tool_module_ships(self, tool):
        assert _pkg.wasm_module(tool).is_file(), f"{tool}.wasm missing from the package"

    @pytest.mark.parametrize("multilib", _MULTILIBS)
    def test_sysroot_has_libgcc_and_libm(self, multilib):
        sysroot = _pkg.sysroot(multilib)
        for lib in ("libgcc.a", "libm.a"):
            assert (sysroot / lib).is_file(), f"{multilib}/{lib} missing from the sysroot"

    def test_flags_table_covers_every_targeted_chip(self):
        table = _flags_table()
        assert len(table) == _EXPECTED_CHIPS, \
            f"cc1_flags.json has {len(table)} chips, expected {_EXPECTED_CHIPS}"
        for chip, row in table.items():
            assert row.get("flags"), f"{chip}: no cc1 flags recorded"

    def test_flags_table_matches_the_driver_chip_table(self):
        """The table and the driver's own chip table name the same chips."""
        wasi = pytest.importorskip("pymcu.toolchain.avr.wasi",
                                   reason="pymcu-avr not installed (needs its chip table)")
        assert set(_flags_table()) == set(wasi._CHIPS)


# ---------------------------------------------------------------------------
# 2. Pipeline the driver runs on the package
# ---------------------------------------------------------------------------

try:
    from rich.console import Console as _Console  # noqa: F401
    from pymcu.toolchain.avr import wasi as _wasi
    from pymcu.toolchain.avr.avrgas import AvrgasToolchain as _AvrgasToolchain
    _HAS_AVRGAS = _wasi.find_wasi_root() is not None
except ImportError:
    _HAS_AVRGAS = False


@pytest.mark.skipif(not _HAS_AVRGAS,
                    reason="pymcu-avr with the WASI toolchain not available (needs wasmtime)")
class TestAvrgasToolchain:
    """AvrgasToolchain end to end, through the WASI pipeline it selects."""

    @pytest.fixture()
    def toolchain(self):
        from rich.console import Console
        from pymcu.toolchain.avr.avrgas import AvrgasToolchain

        AvrgasToolchain._wasi_checked = False  # reset cached detection
        return AvrgasToolchain(Console(quiet=True), chip="atmega328p")

    def test_supports_atmega328p(self, toolchain):
        assert toolchain.supports("atmega328p")

    def test_supports_attiny85(self, toolchain):
        assert toolchain.supports("attiny85")

    def test_the_driver_selects_the_wasi_pipeline(self, toolchain):
        assert toolchain._wasi_pipeline() is not None, \
            "AvrgasToolchain did not select the WASI pipeline"

    def test_is_cached(self, toolchain):
        assert toolchain.is_cached(), "Toolchain reports not cached despite the package being installed"

    def test_compile_c(self, toolchain, tmp_path):
        c_file = tmp_path / "add.c"
        c_file.write_text(_MINIMAL_C)
        objects = toolchain.compile_c(
            c_files=[c_file],
            include_dirs=[],
            cflags=[],
            output_dir=tmp_path,
        )
        assert len(objects) == 1
        assert objects[0].exists()

    def test_full_pipeline(self, toolchain, tmp_path):
        asm_file = tmp_path / "main.s"
        asm_file.write_text(_MINIMAL_ASM)
        c_file = tmp_path / "add.c"
        c_file.write_text(_MINIMAL_C)

        c_objects = toolchain.compile_c(
            c_files=[c_file],
            include_dirs=[],
            cflags=[],
            output_dir=tmp_path,
        )

        # Assemble with the WASI avr-as directly: the AVRA translation the driver applies
        # is for AVRA output, and this source is already GNU AS.
        pipeline = _wasi.WasiAvrPipeline(
            _wasi.WasiAvrTools(_wasi.find_wasi_root()), "atmega328p", 0x800100)
        main_o = pipeline.assemble(asm_file, tmp_path / "main.o")
        assert main_o.exists()

        elf = toolchain.link(
            firmware_obj=main_o,
            c_objects=c_objects,
            output_dir=tmp_path,
        )
        assert elf.exists()

        hex_file = toolchain.elf_to_hex(elf)
        assert hex_file.exists()
        content = hex_file.read_text()
        assert ":00000001FF" in content, "HEX file missing Intel HEX EOF record"
