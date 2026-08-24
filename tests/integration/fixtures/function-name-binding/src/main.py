# PyMCU -- function-name-binding: `f = a`, a function stored in a name.
#
# Regression for PyMCU#69. A function could be PASSED as a Callable argument but not
# stored, and calling the name reported "'f' is not callable (it is a value, not a
# function)" -- which is what the compiler had made of it, not what the program said.
#
# Two names bound to two different functions, because one binding could pass by
# accident if every name collapsed to the last function seen:
#   GPIOR0 (0x3E) = f() = 7
#   GPIOR1 (0x4A) = g() = 9
from pymcu.types import uint8, asm
from pymcu.chips.atmega328p import GPIOR0, GPIOR1


def seven() -> uint8:
    return 7


def nine() -> uint8:
    return 9


def main():
    f = seven
    g = nine
    GPIOR0.value = f()
    GPIOR1.value = g()
    asm("BREAK")
    while True:
        pass
