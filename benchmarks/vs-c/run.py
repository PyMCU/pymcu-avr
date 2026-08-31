#!/usr/bin/env python3
"""Compare PyMCU against avr-gcc -Os on the same programs, with one instrument.

Run it:  python3 benchmarks/vs-c/run.py

WHY THIS EXISTS
    The talk abstract says "zero-cost performance" and mentions "an Arduino's 2 KB of RAM".
    Those are two different claims and only one of them is about total binary size, so the
    numbers have to exist before someone asks in a Q&A rather than during it.

WHAT MAKES THE COMPARISON HONEST, AND WHY EACH PART IS HERE

  ONE SIZE INSTRUMENT FOR BOTH SIDES.  `hex_bytes()` below counts program data out of the
  Intel HEX, and it is the only thing that measures either side. Using the driver's own
  reported figure for PyMCU and avr-size for C would compare two definitions of "size" and
  the difference would look like a result.

  THE .c LIVES BESIDE THE .py.  One directory per program, `src/main.py` and `main.c`. The
  C side is the half nobody audits, so it is put where it cannot be skipped: a C program
  that quietly does less than the Python one is the easiest way to produce a flattering
  number, and the June 2026 measurement of this same example could not be re-checked because
  its C was never kept.

  PROVENANCE IS PRINTED WITH THE NUMBERS.  Package versions, avr-gcc version and the exact
  flags. Without them the table is a number without an experiment two months later.

WHAT IT DOES NOT MEASURE
    Speed. Every figure here is size, in bytes of flash and bytes of RAM.
"""
from __future__ import annotations

import os
import re
import shutil
import subprocess
import sys
from pathlib import Path

HERE = Path(__file__).resolve().parent
PROGRAMS = ["blink", "arith", "loop", "dht-sensor"]

# The flags the comparison is named after. -Os is the point; the section flags let the linker
# drop anything unreferenced, which is what PyMCU's whole-program lowering does by construction,
# so leaving them off would hand PyMCU an advantage it has not earned.
CFLAGS = ["-Os", "-mmcu=atmega328p", "-DF_CPU=16000000UL",
          "-ffunction-sections", "-fdata-sections", "-Wl,--gc-sections"]


def hex_bytes(path: Path) -> int:
    """Bytes of program data in an Intel HEX file. THE instrument, used on both sides."""
    total = 0
    for line in path.read_text().splitlines():
        line = line.strip()
        if not line.startswith(":"):
            continue
        count = int(line[1:3], 16)
        rectype = int(line[7:9], 16)
        if rectype == 0:            # data record; skip EOF and extended-address records
            total += count
    return total


def elf_ram(path: Path) -> int:
    """.data + .bss, i.e. the RAM the program occupies before it runs."""
    out = subprocess.run(["avr-size", "--format=sysv", str(path)],
                         capture_output=True, text=True).stdout
    total = 0
    for line in out.splitlines():
        parts = line.split()
        if len(parts) >= 2 and parts[0] in (".data", ".bss"):
            total += int(parts[1])
    return total


def build_pymcu(d: Path) -> tuple[int, int]:
    shutil.rmtree(d / "dist", ignore_errors=True)
    r = subprocess.run(["pymcu", "build"], cwd=d, capture_output=True, text=True)
    hexf = d / "dist" / "firmware.hex"
    if not hexf.exists():
        raise SystemExit(f"pymcu build failed in {d.name}:\n{r.stdout}\n{r.stderr}")
    elf = d / "dist" / "debug" / "firmware.elf"
    if not elf.exists():
        # Not `else 0`. Zero is the best possible answer in this column and the one the talk
        # quotes, so a missing ELF must not be able to produce it: the failure would read as
        # the result.
        raise SystemExit(f"{d.name}: no ELF at {elf}, cannot measure PyMCU RAM")
    return hex_bytes(hexf), elf_ram(elf)


def build_c(d: Path) -> tuple[int, int]:
    elf, hexf = d / "c.elf", d / "c.hex"
    subprocess.run(["avr-gcc", *CFLAGS, "-o", str(elf), str(d / "main.c")], check=True)
    subprocess.run(["avr-objcopy", "-O", "ihex", "-R", ".eeprom", str(elf), str(hexf)], check=True)
    return hex_bytes(hexf), elf_ram(elf)


def provenance() -> list[str]:
    lines = []
    # Read from the INSTALLED DISTRIBUTION METADATA, in the interpreter that actually runs
    # `pymcu`, not from `pymcu --version`.
    #
    # Those two disagree. Measured on this machine: the --version table prints
    # compiler 0.1.0a3 / avr 0.1.0a1 while the installed distributions are 0.1.0a9 / 0.1.0a5.
    # Whatever the table is reporting, it is not what produced the binary, and a provenance
    # block that records the wrong toolchain is worse than one that records none: the numbers
    # beside it look checked.
    exe = shutil.which("pymcu")
    interp = "python3"
    if exe:
        first = Path(exe).read_text(errors="replace").splitlines()[:1]
        if first and first[0].startswith("#!"):
            interp = first[0][2:].strip()
    probe = (
        "from importlib.metadata import version, PackageNotFoundError\n"
        "for p in ('pymcu-compiler','pymcu-stdlib','pymcu-avr'):\n"
        "    try: print(p, version(p))\n"
        "    except PackageNotFoundError: print(p, '(not installed)')\n"
    )
    got = subprocess.run([interp, "-c", probe], capture_output=True, text=True).stdout
    found = dict(l.split(None, 1) for l in got.splitlines() if l.split())
    for pkg in ("pymcu-compiler", "pymcu-stdlib", "pymcu-avr"):
        lines.append(f"  {pkg:<16} {found.get(pkg, '(unreadable)')}")

    gcc = subprocess.run(["avr-gcc", "--version"], capture_output=True, text=True).stdout
    lines.append(f"  {'avr-gcc':<16} {gcc.splitlines()[0] if gcc else '(missing)'}")
    pin = os.environ.get("PYMCU_BACKEND_BINARY")
    if pin:
        lines.append(f"  {'backend pin':<16} PYMCU_BACKEND_BINARY={pin}")
    lines.append(f"  {'cflags':<16} {' '.join(CFLAGS)}")
    return lines


def main() -> int:
    if shutil.which("avr-gcc") is None:
        print("avr-gcc not found; install the AVR toolchain to run this benchmark.")
        return 1

    print("PyMCU vs avr-gcc -Os, same program on both sides, atmega328p @ 16 MHz\n")
    print("Provenance")
    print("\n".join(provenance()))
    print()
    print(f"{'program':<12} {'PyMCU':>12} {'avr-gcc -Os':>12} {'ratio':>7}   "
          f"{'PyMCU RAM':>10} {'C RAM':>7}")
    print("-" * 68)
    for name in PROGRAMS:
        d = HERE / name
        p_flash, p_ram = build_pymcu(d)
        c_flash, c_ram = build_c(d)
        print(f"{name:<12} {p_flash:>10} B {c_flash:>10} B {p_flash / c_flash:>6.2f}x   "
              f"{p_ram:>8} B {c_ram:>5} B")
    print()
    print("Flash is bytes of program data in the HEX, counted by the same function for both.")
    print("RAM is .data + .bss from the ELF. Neither figure is speed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
