# PyMCU -- name-main-guard: `if __name__ == "__main__": main()`.
#
# Regression for PyMCU#65. The guard is true (the entry file is __main__) and its
# body calls the entry point PyMCU calls itself, which used to be reported as
# "recursive call cycle: main -> main" -- a recursion the program does not contain.
#
# The call is redundant, not doubled: main must run exactly ONCE.
#   GPIOR0 (0x3E) = number of times main ran = 1
#
from pymcu.types import uint8, asm
from pymcu.chips.atmega328p import GPIOR0


def main():
    GPIOR0.value = GPIOR0.value + 1
    asm("BREAK")


if __name__ == "__main__":
    main()
