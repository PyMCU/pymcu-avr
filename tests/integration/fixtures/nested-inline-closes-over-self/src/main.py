# PyMCU#427 -- a nested function decorated @inline, defined inside a method and reading and
# writing `self` of the enclosing method, is the documented way to write a closure
# (docs/language/limitations.md:287). No `nonlocal self` declaration is needed or valid here:
# only self's ATTRIBUTE is mutated, self itself is never rebound.
#
# Nothing forwarded the enclosing method's own `self` binding into the nested function's own
# fresh inline frame, so `self` inside the nested body resolved to an ordinary, never-aliased
# local of that new frame instead of the real instance. Calling bump() had no observable
# effect at all -- not even a wrong non-zero delta.
#
# Expected UART output (CPython: Counter(5).bump_twice() = 5 + 1 + 1): 7
from pymcu.hal.console import print
from pymcu.hal.uart import UART
from pymcu.types import inline, uint8


class Counter:
    def __init__(self, start: uint8):
        self.value = start

    def bump_twice(self):
        @inline
        def bump():
            self.value = self.value + 1
        bump()
        bump()
        return self.value


uart = UART(9600)
c = Counter(5)
print(c.bump_twice())

while True:
    pass
