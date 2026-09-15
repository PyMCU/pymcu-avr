# PyMCU -- float-param-uses: a float parameter, used by one instruction kind at a time.
#
# The backend builds the set of float-typed names to decide which register layout each one
# is spilled with: floats use the GCC layout anchored at R22, integers the one anchored at
# R24. That set was a hand-written list of instruction kinds. A float whose EVERY use was a
# kind the list did not name was spilled as an integer and read back as a float, so the two
# 16-bit halves swap and the value that comes out is not the value that went in.
#
# It was closed once for COMPARISONS (PyMCU#388) and stayed open for the rest. The set asks
# the backend's one exhaustive Val walker now, which is where the knowledge already lived.
#
# Each function below exposes its parameter to exactly ONE kind, so a green build proves
# nothing and only the printed number does.
#
# Expected UART output, which is what CPython prints for the same values:
#   neg -2.25
#   cmp 1
#   add 3.25
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART
from pymcu.types import uint8


def neg_only(t: float) -> float:
    # Unary. Answered -0.0 for every value.
    r: float = -t
    return r


def cmp_only(t: float) -> uint8:
    # A comparison, which is the one case that was closed on its own (#388).
    if t > 0.5:
        return 1
    return 0


def add_only(t: float) -> float:
    # The control: Binary, which the hand-written list always named.
    r: float = t + 1.0
    return r


def main() -> None:
    uart = UART(9600)
    x: float = 2.25

    print("neg", neg_only(x))
    print("cmp", cmp_only(x))
    print("add", add_only(x))

    print("done")
    while True:
        pass
