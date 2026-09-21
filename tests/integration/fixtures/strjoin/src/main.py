# ATmega328P: str.join in expression position -- `print("".join(f"{x:02x}" for x in
# result))`, the shape the Adafruit bus_device simpletest prints a readback register
# with. A generator/comprehension over a compile-time sequence unrolls like the genexp
# reductions do; in a value position the same materializes into a runtime-string buffer.
#
#   a  "" sep over a bytearray, f-string elements with a hex spec
#   b  ", " sep -- the separator written between produced elements
#   c  assignment form, upper-hex spec, then printed from its buffer
#   d  chr(b) elements
#   e  a list of compile-time strings folds to one constant even in expression position
#   f  a runtime `if` filter: the separator counts produced elements, not iterations
#   g  same filter, two elements kept
#   h  materialized value answers len() and prints
#   i  two-clause generator
#   j  a filter that keeps nothing prints the empty string
#
# Expected UART output (115200 via print), matching CPython on the same program:
#   0aff42
#   0a, ff, 42
#   0A-FF-42
#   ABC
#   a.b.c
#   255
#   0a,42
#   7 1025566
#   01012
#   (empty line)
#   DONE
from pymcu.types import uint8
from pymcu.time import delay_ms


def main():
    buf = bytearray(3)
    buf[0] = 0x0A
    buf[1] = 0xFF
    buf[2] = 0x42
    letters = bytearray(3)
    letters[0] = 65
    letters[1] = 66
    letters[2] = 67

    while True:
        # a: the bus_device simpletest shape
        print("".join(f"{x:02x}" for x in buf))

        # b: separator between elements
        print(", ".join(f"{x:02x}" for x in buf))

        # c: assignment form materializes a runtime string
        s = "-".join(f"{x:02X}" for x in buf)
        print(s)

        # d: chr() elements write the byte itself
        print("".join(chr(b) for b in letters))

        # e: a list of compile-time strings folds in expression position
        print(".".join(["a", "b", "c"]))

        # f: runtime filter keeping one element emits no separator
        print(",".join(f"{x}" for x in buf if x > 100))

        # g: runtime filter keeping two
        print(",".join(f"{x:02x}" for x in buf if x != 0xFF))

        # h: the value answers len() and prints its text
        t = "".join(f"{x}" for x in buf)
        print(len(t), t)

        # i: two clauses unroll nested
        print("".join(f"{y}" for x in [2, 3] for y in range(x)))

        # j: nothing produced is the empty string
        print("".join(f"{x}" for x in buf if x == 7))

        print("DONE")
        delay_ms(1000)
