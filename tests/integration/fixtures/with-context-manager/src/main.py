# PyMCU -- with-context-manager: both spellings of `with` over a class.
#
# Regression for PyMCU#101, which was two faults in one statement.
#
# `with o as v:` compiled and left v reading every field as zero. A ZCA __enter__ whose body
# is `return self` has no single runtime value to hand back, so the expansion produced a
# temporary that stood for nothing, the alias binding v to o was dropped, and v read whatever
# that temporary held.
#
# `with V(s) as v:` did not compile at all: nothing bound the manager to a name, so the whole
# statement fell through to "just run the body" and v was reported as never assigned -- on the
# very line that assigns it.
#
# GPIOR0 reads 0 out of reset, so both blocks print 3.
#
# Expected UART output:
#   3
#   3
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8


class V:
    def __init__(self, a: uint8):
        self.a: uint8 = a

    def __enter__(self) -> V:
        return self

    def __exit__(self, a: uint8, b: uint8, c: uint8) -> None:
        pass


def main() -> None:
    s: uint8 = GPIOR0.value + 3

    o = V(s)
    with o as v:
        print(v.a)

    with V(s) as w:
        print(w.a)

    print("done")
