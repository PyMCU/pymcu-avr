# PyMCU -- named-tuple-loop: a tuple of constants past the unroll limit (PyMCU#297) and
# the width the table behind it is stored at (PyMCU#298).
#
# Up to eight constant elements a named tuple binds and the `for` unrolls. At nine it was
# refused at the ASSIGNMENT ("tuples are not supported as runtime values") while the same
# elements written as a list compiled, and so did the same tuple written inline at the
# `for`. Past eight both spellings now take the same storage -- and that storage was one
# byte per element whatever the values were, so a 16-bit table ran on its low byte.
#
# The list arms are here because the tuple is only correct if it agrees with them.
#
# Expected UART (115200):
#   W 256 / W 383 / W 512 / W 16384 / W 16639 / W 32768 / W 32895 / W 33024 / W 49152 / W 65280
#   T 0 / T 7 / T 14 / T 21 / T 28 / T 35 / T 42 / T 49 / T 56
#   L 0 / L 7 / L 14 / L 21 / L 28 / L 35 / L 42 / L 49 / L 56
#   E 100 / E 200 / E 300 / E 400 / E 500 / E 600 / E 700 / E 800 / E 900
#   S 0 / S 7 / S 14 / S 21 / S 28 / S 35 / S 42 / S 49
#   END
from pymcu.hal.console import print


def main():
    wide = (256, 383, 512, 16384, 16639, 32768, 32895, 33024, 49152, 65280)
    for d in wide:
        print("W", d)

    tupled = (0, 7, 14, 21, 28, 35, 42, 49, 56)
    for d in tupled:
        print("T", d)

    listed = [0, 7, 14, 21, 28, 35, 42, 49, 56]
    for d in listed:
        print("L", d)

    evens = (100, 200, 300, 400, 500, 600, 700, 800, 900)
    for d in evens:
        print("E", d)

    short = (0, 7, 14, 21, 28, 35, 42, 49)
    for d in short:
        print("S", d)

    print("END")

    while True:
        pass


main()
