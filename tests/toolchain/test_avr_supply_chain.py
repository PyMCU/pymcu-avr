"""
AVR toolchain supply-chain tests for the pymcu-avr backend.

These tests verify that pymcu-avr-toolchain is correctly installed and that
the AvrgasToolchain pipeline (assemble, compile C, link, objcopy) produces a
valid HEX file.  They are lightweight and run on every CI platform in the
toolchain-smoke matrix (Linux x86_64, Linux arm64, macOS arm64, Windows x64).

Unlike the integration tests (which run the AVR simulator and require the full
PyMCU compiler), these tests only need pymcu-avr-toolchain + pymcu-avr itself.

Run with:
    pytest tests/toolchain/ -v
"""
from __future__ import annotations

import os
import subprocess
import sys
import tempfile
from pathlib import Path

import pytest

# ---------------------------------------------------------------------------
# Toolchain package guard
# ---------------------------------------------------------------------------

try:
    import pymcu_avr_toolchain as _whl
    _BIN_DIR: Path = _whl.get_bin_dir()
    _HAS_WHEEL = True
    _WHEEL_ERR = ""
except Exception as exc:
    _BIN_DIR = Path("/nonexistent")
    _HAS_WHEEL = False
    _WHEEL_ERR = str(exc)

pytestmark = pytest.mark.skipif(
    not _HAS_WHEEL,
    reason=f"pymcu-avr-toolchain not installed: {_WHEEL_ERR}",
)

_IS_WIN = sys.platform == "win32"
_EXE = ".exe" if _IS_WIN else ""

_MINIMAL_C = "int add(int a, int b) { return a + b; }"

_MINIMAL_ASM = """\
.global main
main:
    nop
    ret
"""

_MINIMAL_LD = """\
ENTRY(main)
SECTIONS {
  .text 0x000000 : { *(.vectors) *(.text*) }
  .data 0x800100 : { *(.data*) *(.bss*) *(COMMON) }
}
"""


def _run(*args, env=None) -> subprocess.CompletedProcess:
    return subprocess.run(
        [str(a) for a in args],
        capture_output=True,
        timeout=60,
        env=env,
    )


def _gcc_env() -> dict:
    env = os.environ.copy()
    bin_str = str(_BIN_DIR)
    if bin_str not in env.get("PATH", "").split(os.pathsep):
        env["PATH"] = bin_str + os.pathsep + env.get("PATH", "")
    return env


# ---------------------------------------------------------------------------
# 1. Toolchain validation (mirrors AvrgasToolchain._validate_wheel_gcc)
# ---------------------------------------------------------------------------


class TestToolchainValidation:
    """Tests that reproduce the checks AvrgasToolchain performs before using the wheel."""

    def test_avr_gcc_version_runs(self):
        r = _run(_BIN_DIR / f"avr-gcc{_EXE}", "--version")
        assert r.returncode == 0, f"avr-gcc --version failed:\n{r.stderr.decode()}"

    def test_device_specs_atmega328p(self):
        """Mirrors AvrgasToolchain._validate_wheel_gcc()."""
        r = _run(
            _BIN_DIR / f"avr-gcc{_EXE}",
            "-mmcu=atmega328p",
            "--print-libgcc-file-name",
        )
        assert r.returncode == 0, (
            "Device-specs lookup failed — this is what _validate_wheel_gcc checks.\n"
            "Possible causes: glibc mismatch, missing device-specs, wrong COMPILER_PATH.\n"
            + r.stderr.decode()
        )

    @pytest.mark.skipif(_IS_WIN, reason="execute bits not relevant on Windows")
    def test_libexec_executable(self):
        """cc1/collect2 must be executable or avr-gcc compile step fails silently."""
        libexec = _BIN_DIR.parent / "libexec"
        if not libexec.is_dir():
            pytest.skip("no libexec/ in this toolchain")
        non_exec = [
            str(f)
            for f in libexec.rglob("*")
            if f.is_file() and not f.is_symlink() and not os.access(f, os.X_OK)
        ]
        assert not non_exec, (
            "libexec/ has non-executable files — ZIP artifact upload stripped +x:\n"
            + "\n".join(non_exec[:10])
        )

    @pytest.mark.skipif(_IS_WIN, reason="symlinks not needed on Windows")
    def test_as_symlink_bin(self):
        """bin/as must exist so avr-gcc 15.x finds the right assembler via PATH."""
        assert (_BIN_DIR / "as").exists(), (
            "bin/as symlink missing — avr-gcc 15.x will use system /usr/bin/as (x86_64)"
        )

    @pytest.mark.skipif(_IS_WIN, reason="symlinks not needed on Windows")
    def test_ld_symlink_bin(self):
        """bin/ld must exist so avr-gcc 15.x finds the right linker via PATH."""
        assert (_BIN_DIR / "ld").exists(), (
            "bin/ld symlink missing — collect2 will call system /usr/bin/ld (x86_64)"
        )

    @pytest.mark.skipif(_IS_WIN, reason="symlinks not needed on Windows")
    def test_as_symlink_avr_bin(self):
        """avr/bin/as must exist so COMPILER_PATH lookup succeeds before PATH."""
        avr_bin = _BIN_DIR.parent / "avr" / "bin"
        if not avr_bin.is_dir():
            pytest.skip("no avr/bin/ in this toolchain")
        assert (avr_bin / "as").exists(), (
            "avr/bin/as symlink missing — COMPILER_PATH 'as' lookup fails"
        )


# ---------------------------------------------------------------------------
# 2. AvrgasToolchain integration
# ---------------------------------------------------------------------------

try:
    from rich.console import Console as _Console
    from pymcu.toolchain.avr.avrgas import AvrgasToolchain as _AvrgasToolchain
    _HAS_AVRGAS = True
except ImportError:
    _HAS_AVRGAS = False


@pytest.mark.skipif(not _HAS_AVRGAS, reason="pymcu-avr not installed (needs rich + pymcu-sdk)")
class TestAvrgasToolchain:
    """Test AvrgasToolchain end-to-end using its public API.

    Skipped when pymcu-avr is not installed (toolchain-smoke CI job only has
    pymcu-avr-toolchain + pytest). Runs fully in the integration test environment.
    """

    @pytest.fixture()
    def toolchain(self):
        from rich.console import Console
        from pymcu.toolchain.avr.avrgas import AvrgasToolchain

        AvrgasToolchain._wheel_usable = None  # reset cached validation
        return AvrgasToolchain(Console(quiet=True), chip="atmega328p")

    def test_supports_atmega328p(self, toolchain):
        assert toolchain.supports("atmega328p")

    def test_supports_attiny85(self, toolchain):
        assert toolchain.supports("attiny85")

    def test_validate_wheel_gcc(self, toolchain):
        gcc = str(_BIN_DIR / f"avr-gcc{_EXE}")
        ok = toolchain._validate_wheel_gcc(gcc)
        assert ok, (
            "_validate_wheel_gcc() returned False — device-specs not working on this platform"
        )

    def test_is_cached(self, toolchain):
        assert toolchain.is_cached(), "Toolchain reports not cached despite wheel being installed"

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
        # Translate minimal assembly to GNU AS syntax (no AVRA extensions needed)
        asm_file = tmp_path / "main.s"
        asm_file.write_text(_MINIMAL_ASM)
        c_file = tmp_path / "add.c"
        c_file.write_text(_MINIMAL_C)

        # Compile C
        c_objects = toolchain.compile_c(
            c_files=[c_file],
            include_dirs=[],
            cflags=[],
            output_dir=tmp_path,
        )

        # Assemble (avr-as directly, bypassing _preprocess_asm since asm is already GNU AS)
        avr_as = str(_BIN_DIR / f"avr-as{_EXE}")
        main_o = tmp_path / "main.o"
        r = _run(avr_as, "-mmcu=atmega328p", asm_file, "-o", main_o)
        assert r.returncode == 0, f"avr-as failed:\n{r.stderr.decode()}"

        # Link
        elf = toolchain.link(
            firmware_obj=main_o,
            c_objects=c_objects,
            output_dir=tmp_path,
        )
        assert elf.exists()

        # ELF → HEX
        hex_file = toolchain.elf_to_hex(elf)
        assert hex_file.exists()
        content = hex_file.read_text()
        assert ":00000001FF" in content, "HEX file missing Intel HEX EOF record"
