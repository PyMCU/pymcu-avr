# PyMCU -- bytes-param: a function whose parameter is annotated `bytes` (PyMCU#365).
#
# No part of the compiler had a width for the name `bytes`, so the annotation was taken
# for a CLASS and the function was registered for call-site expansion instead of compiled
# as a subroutine. The expansion then lowered to a debug marker and no statements: the
# function vanished, the assignment of its result vanished with it, and the build said
# [BUILD_OK] and exited 0 on both front ends and every target.
#
# `bytearray` was unaffected, which is what made it easy to walk past: the same program
# with one word changed behaved.
#
# Measured against the baseline compiler, `first`, `third` and `total` are ABSENT from
# firmware.asm and the build is green; the printed numbers still come out right, because
# a call site the expansion can reach is exactly what `main` gives it. So the two halves
# are measured separately: the VALUES here are CPython's for the same bytes, and the
# SYMBOLS are asserted on the assembly, which is where the loss is visible.
#
# Expected UART output:
#   first 5
#   third 7
#   total 18
#   ctl 5
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART
from pymcu.types import uint8

gbuf: uint8[3] = bytearray(3)


def first(buf: bytes) -> uint8:
    # A CONSTANT index, the shape whose loss was silent: the whole function disappeared.
    return buf[0]


def third(buf: bytes) -> uint8:
    return buf[2]


def total(buf: bytes, n: uint8) -> uint8:
    # A RUN-TIME index, so the pointer has to be a real pointer and not a folded constant.
    s: uint8 = 0
    i: uint8 = 0
    while i < n:
        s = s + buf[i]
        i = i + 1
    return s


def first_control(buf: bytearray) -> uint8:
    # The control, one word apart, which behaved all along.
    return buf[0]


uart = UART(9600)
gbuf[0] = 5
gbuf[1] = 6
gbuf[2] = 7

print("first", first(gbuf))
print("third", third(gbuf))
print("total", total(gbuf, 3))
print("ctl", first_control(gbuf))
print("done")

while True:
    pass
