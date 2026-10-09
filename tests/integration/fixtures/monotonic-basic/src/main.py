# monotonic-basic: pymcu.time.monotonic() / monotonic_ns() exist and advance.
#
# `from time import monotonic` used to be an ImportError -- pymcu.time had no
# monotonic()/monotonic_ns() at all, unlike CPython (both) and CircuitPython
# (monotonic). This program importing them at all is half the test (it would
# not have compiled before); the other half is that both read a real,
# auto-armed timebase (built on micros(), already in the timebase-reader
# list) and strictly advance across a real delay, instead of a frozen 0 or a
# value that never changes.
#
# GPIOR0 = 1 iff monotonic() strictly increased across delay_ms(20).
# GPIOR1 = 1 iff monotonic_ns() strictly increased across delay_ms(5).
from pymcu.types import uint8, uint32, asm
from pymcu.chips.atmega328p import GPIOR0, GPIOR1
from pymcu.time import monotonic, monotonic_ns, delay_ms


def main():
    a: float = monotonic()
    delay_ms(20)
    b: float = monotonic()
    moved: uint8 = 0
    if b > a:
        moved = 1
    GPIOR0.value = moved

    n1: uint32 = monotonic_ns()
    delay_ms(5)
    n2: uint32 = monotonic_ns()
    moved_ns: uint8 = 0
    if n2 > n1:
        moved_ns = 1
    GPIOR1.value = moved_ns

    asm("BREAK")
    while True:
        pass
