# PyMCU -- held-read-folds: the other half of PyMCU#183's bargain.
#
# A field of a held instance that NOTHING writes through must stay a compile-time constant.
# The fix for #183 marks only the paths a method actually writes; marking every nested field of
# every held object instead would take a held Pin's _bit out of compile time, and the backend
# needs it as a constant to build the mask -- that is a miscompile, not a lost optimization.
#
# Quiet.peek() only reads, so with GPIOR0 = 7 this reports 7: the value the constructor was
# given, still folded.
from pymcu.types import uint8, asm
from pymcu.chips.atmega328p import GPIOR0, GPIOR1


class Inner:
    def __init__(self, v: uint8):
        self.v: uint8 = v

    def set(self, n: uint8) -> None:
        self.v = n


class Quiet:
    def __init__(self, inner: Inner):
        self.inner = inner

    def peek(self) -> uint8:
        return self.inner.v


quiet = Quiet(Inner(GPIOR0.value))


def main() -> None:
    GPIOR1.value = quiet.peek()
    asm("BREAK")
    while True:
        pass
