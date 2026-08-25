# PyMCU -- outline-dunders: dunder methods marked @outline.
#
# Regression for PyMCU#98. Marking a dunder @outline changed the answer, and the reason was
# not the emission: the body IS emitted, and every operator lowering looked the method up
# directly in inlineFunctions, where an outlined method is absent by construction. The guard
# came out false, the operator fell through to its built-in meaning applied to the instance
# handle, and the handle is zero. Seven different wrong answers were seven fallbacks over
# that same zero; __len__ and __contains__ reached a built-in with a type check and failed to
# build instead, which was the better of the two outcomes.
#
# __call__ was the control that decided it: the one dunder that outlines AND answers, and the
# one that resolves through the helper which consults the method ASTs before inlineFunctions.
#
# GPIOR0 reads 0 out of reset, so a starts at 5.
#
# Expected UART output:
#   6          len(b)      -> a + 1
#   7          b[2]        -> a + 2
#   11         b[0]        -> a is 10 + 1 after the store
#   in         11 in b
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint8, outline


class Box:
    def __init__(self, a: uint8):
        self.a: uint8 = a

    @outline
    def __len__(self) -> uint8:
        return self.a + 1

    @outline
    def __getitem__(self, i: uint8) -> uint8:
        return self.a + i

    @outline
    def __setitem__(self, i: uint8, v: uint8):
        self.a = v + i

    @outline
    def __contains__(self, v: uint8) -> bool:
        return v == self.a


def main():
    s: uint8 = GPIOR0.value + 5
    b = Box(s)
    print(len(b))
    print(b[2])
    b[1] = 10
    print(b[0])
    if 11 in b:
        print("in")
    else:
        print("out")
    print("done")
