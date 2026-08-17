# -----------------------------------------------------------------------------
# PyMCU CLI Driver
# Copyright (C) 2026 Ivan Montiel Cardona and the PyMCU Project Authors
#
# SPDX-License-Identifier: MIT
#
# This program is free software: you can redistribute it and/or modify
# it under the terms of the GNU Affero General Public License as published
# by the Free Software Foundation, either version 3 of the License, or
# (at your option) any later version.
#
# This program is distributed in the hope that it will be useful,
# but WITHOUT ANY WARRANTY; without even the implied warranty of
# MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
# GNU Affero General Public License for more details.
#
# You should have received a copy of the GNU Affero General Public License
# along with this program.  If not, see <https://www.gnu.org/licenses/>.
# -----------------------------------------------------------------------------

"""
WASI backend for the AVR toolchain.

Runs avr-as, avr-ld and avr-objcopy as wasm32-wasip1 modules under wasmtime
instead of as native executables, so a single architecture-independent wheel
replaces one native build per platform.

All five tools travel in one package -- avr-as, avr-ld, avr-objcopy, cc1 and
cc1plus -- because cc1_flags.json has to match the libgcc.a and the binutils
published with it; splitting them would only create two projects forced to share
a version.  When the package (or the wasmtime runtime) is absent, AvrgasToolchain
falls back to the native avr-gcc.

avr-gcc is normally the linker driver; here the linker command line it would
have produced is built directly.  Two chip-derived values are needed and both
come from tables verified against `avr-gcc -mmcu=<chip> -###`:

  -m<emulation>   the BFD emulation (avr4/avr5/avr6/avr25/avr51)
  -Tdata          0x800000 + RAMSTART, already known to AvrgasToolchain
"""

from __future__ import annotations

import os
import platform
import shutil
import tempfile
from pathlib import Path
from typing import Optional

# Chip -> (BFD emulation, library subdirectory). Both asked of the compiler,
# never derived from the family name, because neither follows from it:
#
#   atmega1280  is avr51, not avr6 -- the family name says nothing.
#   attiny13    is avr25 to the linker but links against avr25/tiny-stack, the
#               8-bit-stack-pointer variant, while attiny85 links against plain
#               avr25. Using the wrong one emits code that writes SPH on a part
#               that has no SPH.
# The list is exactly PyMCU's supported AVR chips (lib/src/pymcu/chips/), and
# every one of them resolves to a multilib the wheel still ships: the toolchain
# build prunes to KEEP_MULTILIBS="avr25 avr4 avr5 avr6" (avr25/tiny-stack rides
# along inside avr25). Adding a chip on another core -- an avr51 part, say --
# means restoring that multilib in avr-gcc-build/scripts/build-avr-*.sh as well
# as adding a row here. Forgetting the multilib is loud, not silent: the link
# fails with "skipping incompatible" and "cannot find -lgcc".
#
# Regenerate with gen_cc1_table.py, which asks avr-gcc for all three.
_CHIPS: dict[str, tuple[str, str]] = {
    "atmega168": ("avr5", "avr5"),
    "atmega168p": ("avr5", "avr5"),
    "atmega2560": ("avr6", "avr6"),
    "atmega328": ("avr5", "avr5"),
    "atmega328p": ("avr5", "avr5"),
    "atmega32u4": ("avr5", "avr5"),
    "atmega48": ("avr4", "avr4"),
    "atmega48p": ("avr4", "avr4"),
    "atmega88": ("avr4", "avr4"),
    "atmega88p": ("avr4", "avr4"),
    "attiny13": ("avr25", "avr25/tiny-stack"),
    "attiny13a": ("avr25", "avr25/tiny-stack"),
    "attiny2313": ("avr25", "avr25/tiny-stack"),
    "attiny24": ("avr25", "avr25/tiny-stack"),
    "attiny25": ("avr25", "avr25/tiny-stack"),
    "attiny4313": ("avr25", "avr25"),
    "attiny44": ("avr25", "avr25"),
    "attiny45": ("avr25", "avr25"),
    "attiny84": ("avr25", "avr25"),
    "attiny85": ("avr25", "avr25"),
}
_DEFAULT = ("avr5", "avr5")

_TOOLS = ("avr-as", "avr-ld", "avr-objcopy")

_warmed = False


def _announce_warmup() -> None:
    """Say that the first run is preparing the toolchain, once.

    Compiling the wasm modules to native code takes a few seconds and happens
    only the first time on a machine. Without a word it reads as the tool
    hanging, which is exactly how it was reported from a live demo.
    """
    global _warmed
    if _warmed:
        return
    _warmed = True
    import sys as _sys  # noqa: PLC0415
    print("Preparing the AVR toolchain for this machine (first run only)...",
          file=_sys.stderr, flush=True)


def emulation_for(chip: str) -> str:
    """The BFD emulation passed to avr-ld as -m<emulation>."""
    return _CHIPS.get(chip.lower(), _DEFAULT)[0]


def multilib_for(chip: str) -> str:
    """The libgcc/avr-libc subdirectory. Not always the emulation name."""
    return _CHIPS.get(chip.lower(), _DEFAULT)[1]


def is_known_chip(chip: str) -> bool:
    return chip.lower() in _CHIPS


class WasiUnavailable(RuntimeError):
    """Raised when the WASI toolchain cannot be used on this machine."""


def _cache_dir() -> Path:
    import wasmtime  # noqa: PLC0415

    version = getattr(wasmtime, "__version__", "")
    if not version:
        from importlib.metadata import version as pkg_version  # noqa: PLC0415

        version = pkg_version("wasmtime")
    base = os.environ.get("XDG_CACHE_HOME") or (Path.home() / ".cache")
    key = f"{platform.system().lower()}-{platform.machine()}-wasmtime{version}"
    return Path(base) / "pymcu" / "avr-wasi" / key


class WasiAvrTools:
    """Loads the three wasm modules once and runs them in a fresh instance each
    time.  These are single-shot CLI programs that end in proc_exit, so their
    linear memory cannot be reused between invocations; compiling the module is
    the expensive part and it happens once per process (and is cached on disk
    across runs, keyed by OS, CPU and wasmtime version).
    """

    def __init__(self, root: Path) -> None:
        try:
            from wasmtime import Engine, Linker, Module  # noqa: PLC0415
        except ImportError as exc:  # pragma: no cover - depends on install extras
            raise WasiUnavailable(
                "the wasmtime package is required to run the WASI AVR toolchain"
            ) from exc

        self.root = root
        self._engine = Engine()
        self._linker = Linker(self._engine)
        self._linker.define_wasi()
        self._modules: dict[str, object] = {}

        cache = _cache_dir()
        try:
            cache.mkdir(parents=True, exist_ok=True)
        except OSError:
            cache = None

        for name in _TOOLS:
            wasm = root / f"{name}.wasm"
            if not wasm.exists():
                raise WasiUnavailable(f"{wasm} not found")
            cached = cache / f"{name}.cwasm" if cache is not None else None
            module = None
            if cached is not None and cached.exists():
                try:
                    if cached.stat().st_mtime >= wasm.stat().st_mtime:
                        module = Module.deserialize_file(self._engine, str(cached))
                except Exception:
                    # A stale or foreign cache entry is not an error: recompile.
                    module = None
            if module is None:
                _announce_warmup()
                module = Module.from_file(self._engine, str(wasm))
                if cached is not None:
                    try:
                        cached.write_bytes(module.serialize())
                    except OSError:
                        pass
            self._modules[name] = module

    def run(self, tool: str, argv: list[str], preopens: list[tuple[Path, str]]) -> None:
        from wasmtime import ExitTrap, Store, WasiConfig  # noqa: PLC0415

        # The capture files stay outside the preopened directory so they cannot
        # become link inputs, and are read after the store is dropped: Windows
        # refuses to delete a file whose handle is still open.
        with tempfile.TemporaryDirectory(prefix="pymcu-avr-wasi-") as tmp:
            out = Path(tmp) / "stdout"
            err = Path(tmp) / "stderr"

            config = WasiConfig()
            config.argv = [tool, *argv]
            for host, guest in preopens:
                config.preopen_dir(str(host), guest)
            config.stdout_file = str(out)
            config.stderr_file = str(err)

            store = Store(self._engine)
            store.set_wasi(config)

            code = 0
            try:
                instance = self._linker.instantiate(store, self._modules[tool])
                instance.exports(store)["_start"](store)
            except ExitTrap as exc:
                code = exc.code
            del store

            def read(path: Path) -> str:
                if not path.exists():
                    return ""
                return path.read_bytes().decode("utf-8", errors="replace")

            stdout, stderr = read(out), read(err)

        if code != 0:
            raise RuntimeError(f"{tool} failed:\n{stderr or stdout}")


def find_wasi_root() -> Optional[Path]:
    """Locate the directory holding the three .wasm modules."""
    override = os.environ.get("PYMCU_AVR_WASI_ROOT")
    if override:
        root = Path(override)
        return root if all((root / f"{t}.wasm").exists() for t in _TOOLS) else None
    try:
        import pymcu_avr_toolchain_wasi as pkg  # noqa: PLC0415
    except ImportError:
        return None
    root = Path(pkg.__file__).parent / "wasm"
    return root if all((root / f"{t}.wasm").exists() for t in _TOOLS) else None


def sysroot_dir(root: Path, multilib: str) -> Optional[Path]:
    """The libgcc.a / libm.a pair for one multilib, shipped next to the modules.

    Keyed by multilib, not by emulation: attiny13 and attiny85 are both avr25 to
    the linker but need different libgcc.a (avr25/tiny-stack vs avr25).
    """
    bases = [root.parent / "sysroot"]
    override = os.environ.get("PYMCU_AVR_WASI_SYSROOT")
    if override:
        bases.append(Path(override))
    for base in bases:
        candidate = base / multilib
        if (candidate / "libgcc.a").exists() and (candidate / "libm.a").exists():
            return candidate
    return None


_FFI_TOOLS = ("cc1", "cc1plus")
_CPP_SUFFIXES = {".cpp", ".cc", ".cxx", ".C"}


def find_ffi_root() -> Optional[Path]:
    """Locate the directory holding cc1.wasm / cc1plus.wasm.

    Same package and same directory as the other three; kept as its own lookup
    so an older or partial install without the front ends degrades to the native
    compiler instead of failing.
    """
    override = os.environ.get("PYMCU_AVR_WASI_FFI_ROOT") or os.environ.get(
        "PYMCU_AVR_WASI_ROOT")
    if override:
        root = Path(override)
    else:
        root = find_wasi_root()
        if root is None:
            return None
    return root if all((root / f"{t}.wasm").exists() for t in _FFI_TOOLS) else None


class WasiFfiCompiler:
    """cc1 / cc1plus on wasm, replacing avr-gcc for the compile step.

    avr-gcc's driver turns -mmcu=<chip> into a cc1 command line through its spec
    machinery.  That expansion is reproduced here from a table generated by
    asking the compiler itself (`avr-gcc -mmcu=<chip> -###`), because the device
    macro casing (-D__AVR_ATmega328P__) and -mn-flash do not follow from the chip
    string.  See gen_cc1_table.py in the avr-wasi repository.
    """

    def __init__(self, root: Path, chip: str) -> None:
        try:
            from wasmtime import Engine, Linker, Module  # noqa: PLC0415
        except ImportError as exc:  # pragma: no cover
            raise WasiUnavailable("wasmtime is required for the WASI C compiler") from exc

        import json  # noqa: PLC0415

        self.root = root
        self.chip = chip.lower()

        table_path = root.parent / "cc1_flags.json"
        if not table_path.exists():
            override = os.environ.get("PYMCU_AVR_WASI_FLAGS")
            if override:
                table_path = Path(override)
        if not table_path.exists():
            raise WasiUnavailable(f"{table_path} not found")
        table = json.loads(table_path.read_text())
        if self.chip not in table:
            raise WasiUnavailable(f"{chip} has no cc1 flags in {table_path.name}")
        self.flags: list[str] = list(table[self.chip]["flags"])

        self.include_root = root.parent / "include"
        for sub in ("gcc", "gcc-fixed", "avr"):
            if not (self.include_root / sub).is_dir():
                raise WasiUnavailable(f"{self.include_root / sub} not found")

        self._engine = Engine()
        self._linker = Linker(self._engine)
        self._linker.define_wasi()
        cache = _cache_dir()
        try:
            cache.mkdir(parents=True, exist_ok=True)
        except OSError:
            cache = None
        self._modules = {}
        for name in _FFI_TOOLS:
            wasm = root / f"{name}.wasm"
            cached = cache / f"{name}.cwasm" if cache is not None else None
            module = None
            if cached is not None and cached.exists():
                try:
                    if cached.stat().st_mtime >= wasm.stat().st_mtime:
                        module = Module.deserialize_file(self._engine, str(cached))
                except Exception:
                    module = None
            if module is None:
                _announce_warmup()
                module = Module.from_file(self._engine, str(wasm))
                if cached is not None:
                    try:
                        cached.write_bytes(module.serialize())
                    except OSError:
                        pass
            self._modules[name] = module

    def _run(self, tool: str, argv: list[str], work: Path) -> None:
        from wasmtime import ExitTrap, Store, WasiConfig  # noqa: PLC0415

        with tempfile.TemporaryDirectory(prefix="pymcu-avr-cc1-") as tmp:
            out, err = Path(tmp) / "stdout", Path(tmp) / "stderr"
            config = WasiConfig()
            config.argv = [tool, *argv]
            config.preopen_dir(str(work), "/work")
            config.preopen_dir(str(self.include_root), "/inc")
            config.stdout_file, config.stderr_file = str(out), str(err)
            store = Store(self._engine)
            store.set_wasi(config)
            code = 0
            try:
                instance = self._linker.instantiate(store, self._modules[tool])
                instance.exports(store)["_start"](store)
            except ExitTrap as exc:
                code = exc.code
            del store

            def read(path: Path) -> str:
                return path.read_bytes().decode("utf-8", errors="replace") if path.exists() else ""

            stdout, stderr = read(out), read(err)
        if code != 0:
            raise RuntimeError(f"{tool} failed:\n{stderr or stdout}")

    def compile(
        self,
        sources: list[Path],
        include_dirs: list[Path],
        cflags: list[str],
        output_dir: Path,
    ) -> list[Path]:
        """Compile each source to assembly and hand the .s files back.

        Nothing from the host filesystem is passed through: the user's include
        directories and sources are copied into a scratch tree that is preopened
        as /work, so absolute paths, drive letters and symlinks never reach the
        module.  Include directories keep their internal structure, so nested
        includes such as "sub/thing.h" still resolve.
        """
        work = output_dir / ".wasi-ffi"
        shutil.rmtree(work, ignore_errors=True)
        (work / "src").mkdir(parents=True)

        guest_includes = ["/work/src"]
        for index, directory in enumerate(include_dirs):
            if not directory.is_dir():
                continue
            dest = work / f"inc{index}"
            shutil.copytree(directory, dest)
            guest_includes.append(f"/work/inc{index}")

        # Headers sitting next to a source are found through the source's own
        # directory by the real compiler, so reproduce that by putting them in
        # the same scratch directory as the source.
        for source in sources:
            for sibling in source.parent.iterdir():
                if sibling.is_file() and sibling.suffix in {".h", ".hpp", ".hh", ".inc"}:
                    shutil.copy(sibling, work / "src" / sibling.name)

        user_flags = [f for f in cflags if not f.startswith("-I")]

        results: list[Path] = []
        for source in sources:
            shutil.copy(source, work / "src" / source.name)
            is_cpp = source.suffix in _CPP_SUFFIXES
            tool = "cc1plus" if is_cpp else "cc1"
            extra = ["-fno-exceptions", "-fno-rtti", "-std=c++17"] if is_cpp else []
            asm_name = source.stem + ".s"
            self._run(tool, [
                "-quiet", "-nostdinc",
                "-isystem", "/inc/gcc", "-isystem", "/inc/gcc-fixed", "-isystem", "/inc/avr",
                *[f"-I{d}" for d in guest_includes],
                *self.flags, "-mno-skip-bug", "-Os", *user_flags, *extra,
                f"/work/src/{source.name}", "-o", f"/work/{asm_name}",
            ], work)
            results.append(work / asm_name)
        return results


class WasiAvrPipeline:
    """assemble / link / elf_to_hex, mirroring AvrgasToolchain's three steps.

    Files are reached through preopened directories -- the build directory as
    /work and the sysroot as /sys -- so no host path ever crosses into the
    module.  That is what makes Windows drive letters and backslashes moot.
    """

    def __init__(self, tools: WasiAvrTools, chip: str, data_origin: int,
                 ffi_factory=None) -> None:
        self.tools = tools
        # Built on the first compile_c, never before. Constructing it compiles
        # cc1 and cc1plus, which are 48 of the 50 MB of modules, and a project
        # with no C sources never calls them: doing it eagerly added ~3 s of
        # silence to every first build and 100 MB of cache for nothing.
        self._ffi_factory = ffi_factory
        self._ffi = None
        self._ffi_built = False
        self.chip = chip
        self.emulation = emulation_for(chip)
        self.multilib = multilib_for(chip)
        self.data_origin = data_origin
        if not is_known_chip(chip):
            # Guessing here is how attiny13 would end up linked against the
            # 16-bit-stack libgcc; refuse instead and let the native path run.
            raise WasiUnavailable(
                f"{chip} is not in the verified chip table; "
                "add it with gen_cc1_table.py or build with PYMCU_AVR_WASI=0"
            )

    @property
    def ffi(self):
        if not self._ffi_built:
            self._ffi_built = True
            self._ffi = self._ffi_factory() if self._ffi_factory is not None else None
        return self._ffi

    def _sysroot(self) -> Path:
        sysroot = sysroot_dir(self.tools.root, self.multilib)
        if sysroot is None:
            raise WasiUnavailable(
                f"no AVR sysroot for multilib {self.multilib} "
                f"(expected libgcc.a and libm.a)"
            )
        return sysroot

    def compile_c(
        self,
        sources: list[Path],
        include_dirs: list[Path],
        cflags: list[str],
        output_dir: Path,
    ) -> list[Path]:
        """Compile C/C++ sources to .o, going through cc1/cc1plus then avr-as.

        Raises WasiUnavailable when the [ffi] extra is not installed, which is
        the signal for AvrgasToolchain to use the native avr-gcc instead.
        """
        if self.ffi is None:
            raise WasiUnavailable(
                "cc1/cc1plus are not available in this toolchain installation")

        asm_files = self.ffi.compile(sources, include_dirs, cflags, output_dir)
        work = asm_files[0].parent if asm_files else output_dir

        objects: list[Path] = []
        for asm in asm_files:
            obj_name = asm.stem + ".o"
            self.tools.run(
                "avr-as",
                [f"-mmcu={self.chip}", "-mno-skip-bug",
                 f"/work/{asm.name}", "-o", f"/work/{obj_name}"],
                [(work, "/work")],
            )
            destination = output_dir / obj_name
            shutil.move(str(work / obj_name), destination)
            objects.append(destination)
        shutil.rmtree(work, ignore_errors=True)
        return objects

    def assemble(self, asm_file: Path, obj_out: Optional[Path] = None) -> Path:
        obj = obj_out if obj_out is not None else asm_file.with_suffix(".o")
        if obj.parent != asm_file.parent:
            raise WasiUnavailable(
                "the object file must sit next to the assembly source "
                f"({obj.parent} != {asm_file.parent})"
            )
        self.tools.run(
            "avr-as",
            [
                f"-mmcu={self.chip}", "-mno-skip-bug",
                f"/work/{asm_file.name}", "-o", f"/work/{obj.name}",
            ],
            [(asm_file.parent, "/work")],
        )
        return obj

    def link(self, firmware_obj: Path, output_dir: Path, linker_script: Path,
             extra_objects: "Optional[list[Path]]" = None) -> Path:
        sysroot = self._sysroot()
        obj = firmware_obj
        if obj.parent != output_dir:
            obj = output_dir / firmware_obj.name
            shutil.copy(firmware_obj, obj)

        extras = []
        for other in extra_objects or []:
            if other.parent != output_dir:
                copied = output_dir / other.name
                shutil.copy(other, copied)
                other = copied
            extras.append(other.name)
        if linker_script.parent != output_dir:
            copied = output_dir / linker_script.name
            shutil.copy(linker_script, copied)
            linker_script = copied

        elf = output_dir / "firmware.elf"
        self.tools.run(
            "avr-ld",
            [
                f"-m{self.emulation}",
                "-Tdata", f"0x{self.data_origin:06X}",
                "--relax",
                "-o", f"/work/{elf.name}",
                "-L/sys",
                f"/work/{obj.name}", *[f"/work/{n}" for n in extras], "-lm", "-lgcc",
                "-T", f"/work/{linker_script.name}",
            ],
            [(output_dir, "/work"), (sysroot, "/sys")],
        )
        return elf

    def elf_to_hex(self, elf_file: Path) -> Path:
        hex_file = elf_file.with_suffix(".hex")
        self.tools.run(
            "avr-objcopy",
            [
                "-O", "ihex", "-R", ".eeprom",
                f"/work/{elf_file.name}", f"/work/{hex_file.name}",
            ],
            [(elf_file.parent, "/work")],
        )
        return hex_file
