# ATmega328P: integer overflow wraps two's-complement at the STORE into a typed
# variable. Arithmetic promotes in width, so nothing is clamped mid-expression;
# what truncates is writing the result back into a variable of a fixed width.
# An int8 at 127 therefore steps to -128 (0x7F + 1 = 0x80), and a uint8 at 255
# steps to 0. This is a deliberate divergence from CPython, whose ints are
# unbounded and would keep counting 128, 129, ...: PyMCU counters are machine
# words, and a firmware that relies on a counter growing past its width is
# relying on something an MCU cannot do.
#
# Every counter is seeded from GPIOR0 (0 at reset, opaque to the constant
# folder) and stepped inside a loop, so each crossing is computed at runtime
# rather than folded at compile time. Both assignment forms are covered:
# `n = n + 1` and `n += 1` take different codegen paths.
#
# Expected UART output (9600 baud):
#   WRAP
#   A          int8, n = n + 1, from 125
#   126
#   127
#   -128
#   -127
#   Y          the value stored right after 127 compares equal to -128
#   B          int8, n += 1, from 125
#   126
#   127
#   -128
#   -127
#   C          int8, n = n - 1, from -126
#   -127
#   -128
#   127
#   D          int16, n = n + 1, from 32765
#   32766
#   32767
#   -32768
#   -32767
#   E          int16, n -= 1, from -32766
#   -32767
#   -32768
#   32767
#   F          uint8, n += 1, from 253
#   254
#   255
#   0
#   1
#   G          uint8, n -= 1, from 1
#   0
#   255
#   254
#   H          uint16, n += 1, from 65533
#   65534
#   65535
#   0
#   1
#   I          uint16, n -= 1, from 1
#   0
#   65535
#   65534
#   J          int8 counter in a `while True`, from 120, ten prints
#   121
#   122
#   123
#   124
#   125
#   126
#   127
#   -128
#   -127
#   -126
#   END
from pymcu.types import uint8, int8, int16, uint16
from pymcu.hal.uart import UART
from pymcu.chips.atmega328p import GPIOR0


def main():
    uart = UART(9600)
    uart.println("WRAP")

    seed: uint8 = GPIOR0.value

    uart.println("A")
    a: int8 = 125 + seed
    i: uint8 = 0
    while i < 4:
        a = a + 1
        uart.print_int16(a)
        if i == 2:
            if a == -128:
                uart.println("Y")
            else:
                uart.println("N")
        i = i + 1

    uart.println("B")
    b: int8 = 125 + seed
    i = 0
    while i < 4:
        b += 1
        uart.print_int16(b)
        i = i + 1

    uart.println("C")
    c: int8 = 0 - 126 - seed
    i = 0
    while i < 3:
        c = c - 1
        uart.print_int16(c)
        i = i + 1

    uart.println("D")
    d: int16 = 32765 + seed
    i = 0
    while i < 4:
        d = d + 1
        uart.print_int16(d)
        i = i + 1

    uart.println("E")
    e: int16 = 0 - 32766 - seed
    i = 0
    while i < 3:
        e -= 1
        uart.print_int16(e)
        i = i + 1

    uart.println("F")
    f: uint8 = 253 + seed
    i = 0
    while i < 4:
        f += 1
        uart.print_byte(f)
        i = i + 1

    uart.println("G")
    g: uint8 = 1 + seed
    i = 0
    while i < 3:
        g -= 1
        uart.print_byte(g)
        i = i + 1

    uart.println("H")
    h: uint16 = 65533 + seed
    i = 0
    while i < 4:
        h += 1
        uart.print_uint16(h)
        i = i + 1

    uart.println("I")
    j: uint16 = 1 + seed
    i = 0
    while i < 3:
        j -= 1
        uart.print_uint16(j)
        i = i + 1

    uart.println("J")
    k: int8 = 120 + seed
    shown: uint8 = 0
    while True:
        k += 1
        uart.print_int16(k)
        shown += 1
        if shown == 10:
            break

    uart.println("END")

    while True:
        pass
