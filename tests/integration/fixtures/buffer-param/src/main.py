# PyMCU -- buffer-param: indexing an UNANNOTATED buffer parameter.
#
# `buf[i]` on a parameter with no type annotation used to be read as a REGISTER BIT
# index rather than an element index. With a run-time index that failed to build
# ("Bit index must be constant for reading", a message naming neither the buffer nor
# the parameter, about an operation the program does not contain). With a CONSTANT
# index it was worse: it compiled, silently, into a bit test of the buffer's ADDRESS,
# so the program ran and answered 0 or 1 where a byte was expected.
#
# The callers were never the problem -- an array argument is passed by its base
# address either way. So what these checkpoints measure is VALUES, because a wrong
# answer is the shape this bug had.
#
# Data-space addresses (ATmega328P): GPIOR0 = 0x3E, GPIOR1 = 0x4A, GPIOR2 = 0x4B
#
# Checkpoints:
#   1. run-time index, MODULE-level buffer   -> 5+6+7 = 18, and buf[0] = 5
#   2. run-time index, LOCAL buffer          -> 1+2+3 = 6,  and buf[2] = 3
#   3. the callee WRITES through the pointer -> the caller sees the bytes
#   4. an @inline callee, both buffer kinds  -> same answers as the outlined one
#
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2
from pymcu.types import uint8, inline, asm

gbuf: uint8[3] = bytearray(3)


def total(buf, n: uint8) -> uint8:
    s: uint8 = 0
    i: uint8 = 0
    while i < n:
        s = s + buf[i]
        i = i + 1
    return s


def first(buf) -> uint8:
    # A CONSTANT index: the shape that used to compile into a bit test of the address.
    return buf[0]


def third(buf) -> uint8:
    return buf[2]


def fill(buf, n: uint8):
    i: uint8 = 0
    while i < n:
        buf[i] = i + 10
        i = i + 1


@inline
def total_inline(buf, n: uint8) -> uint8:
    s: uint8 = 0
    i: uint8 = 0
    while i < n:
        s = s + buf[i]
        i = i + 1
    return s


def main():
    gbuf[0] = 5
    gbuf[1] = 6
    gbuf[2] = 7

    # --- Checkpoint 1: module-level buffer through an outlined callee ---
    GPIOR0.value = total(gbuf, 3)     # 18
    GPIOR1.value = first(gbuf)        # 5
    asm("BREAK")

    # --- Checkpoint 2: local buffer, same callees ---
    lbuf: uint8[3] = [1, 2, 3]
    GPIOR0.value = total(lbuf, 3)     # 6
    GPIOR1.value = third(lbuf)        # 3
    asm("BREAK")

    # --- Checkpoint 3: the callee writes through the pointer it was handed ---
    fill(gbuf, 3)                     # gbuf becomes 10, 11, 12
    GPIOR0.value = gbuf[0]
    GPIOR1.value = gbuf[2]
    GPIOR2.value = total(gbuf, 3)     # 33
    asm("BREAK")

    # --- Checkpoint 4: an @inline callee answers the same for both buffer kinds ---
    GPIOR0.value = total_inline(gbuf, 3)    # 33
    GPIOR1.value = total_inline(lbuf, 3)    # 6
    asm("BREAK")

    while True:
        pass
