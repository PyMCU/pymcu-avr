# PyMCU -- entry-main-explicit-call: main() written at module level (PyMCU#301).
#
# `main()` is where the entry point's body runs. It used to be dropped, so every
# module-level statement ran first and the program printed C before A: the body was
# still in the firmware, just in the wrong place, and nothing said so.
#
# CPython prints B, A 3, C, END for this file. So does the firmware.
from pymcu.hal.console import print
from pymcu.types import uint8


def main():
    x: uint8 = 3
    print("A", x)


print("B")
main()
print("C")
print("END")
