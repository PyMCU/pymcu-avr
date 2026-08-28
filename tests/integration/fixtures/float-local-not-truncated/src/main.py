# PyMCU -- float-local-not-truncated: an unannotated local bound to a float literal.
#
# Regression for PyMCU#216. `f = 1.5` with no annotation was typed uint8, so the store
# truncated and `print(f)` wrote 1. Two lines, no error, no warning.
#
# The type-inference chain in EmitScalarVarAssign had a case for a temporary, a variable, an
# integer literal and a cast, and none for a float constant -- so a float literal fell past
# every branch and kept the UINT8 default.
#
# Three things had to coincide for it to survive: no annotation, a bare float literal, and no
# float arithmetic between the binding and the read. `f: uint8 = 1.5` is refused outright as a
# TypeError, `f: float = 1.5` was always correct, and `g = f + 1.0` types its result from the
# arithmetic. That is why a codebase full of floats did not notice.
#
# Expected UART output:
#   1.5      f = 1.5                       discriminator, printed 1 before
#   3.75     f = 3.75                      discriminator, printed 3 before
#   2.5      rebound to another literal    discriminator, printed 2 before
#   4.0      f: float = 1.5 then + 2.5     control, always correct
#   7        n = 7                         control, an integer local must stay an integer
#   1        uint8(1.5) explicit cast      control, explicit truncation is still truncation
#   done     terminator
from pymcu.hal.console import print
from pymcu.types import uint8


def main():
    a = 1.5
    print(a)

    b = 3.75
    print(b)

    c = 1.5
    c = 2.5
    print(c)

    d: float = 1.5
    print(d + 2.5)

    n = 7
    print(n)

    t = uint8(1.5)
    print(t)

    print("done")
