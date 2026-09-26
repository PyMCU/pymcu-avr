# expect: match
# doc: https://github.com/PyMCU/PyMCU/issues/520
# The same expression three times in one program: at module level, inside a plain function,
# and at module level again. CPython prints 2, 2, 2. The emulator printed 2, 0, 2 until
# PyMCU b574801a gave a nested field of a module-level instance its global storage.
#
# Written as one program rather than a matched pair on purpose. Nobody has to be convinced
# that two programs are equivalent, because it is the same line written three times in the
# same file, and only the one inside the function is wrong.
#
# The trigger is two levels with an instance in the middle, read from a real subroutine.
# Either half alone is correct: 427 covers the same read at module level, and the same read
# from a method or from an @inline function is correct too.
from pymcu.types import uint8
from pymcu.chips.atmega328p import GPIOR0


class Src:
    def __init__(self, base: uint8):
        self.base: uint8 = base


class Holder:
    def __init__(self, n: uint8):
        self.inner: Src = Src(n + GPIOR0.value)


h2 = Holder(2)


def read_it():
    return h2.inner.base


print(h2.inner.base)
print(read_it())
print(h2.inner.base)
print("END")
