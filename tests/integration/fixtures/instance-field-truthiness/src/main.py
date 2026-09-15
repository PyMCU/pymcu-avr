# A field holding an instance is true or false the way its class says (PyMCU#385).
#
# `if obj:`, `not obj` and `while not obj:` were rewritten to the object's __bool__ or
# __len__ only when the object was a NAME. A FIELD fell through to the numeric path, which
# tests whatever the instance collapsed to, so `while not self._echo:` inside a method ran
# forever against an object whose __len__ says it is not empty -- the wait loop every
# CircuitPython driver writes.
#
# The second half is why a plain rewrite was not enough. A method reading a field that holds
# an INSTANCE cannot be compiled as a shared body: it receives the field as a number and has
# no `self` at all (its parameter is `self__field`), so nothing the body does with the
# instance has a receiver. `self.field.<anything>` was already kept out of that path; a BARE
# read of the same field was not, because the layout types a constructor-assigned field
# uint8. Such a method is expanded at its call sites now, which is where `self` is bound.
#
# `poll()` is the re-evaluation half: __len__ counts its own calls and answers 0, 0, then 1,
# so a loop that reads it once and keeps the answer never leaves, and one that re-reads it
# leaves on the third turn. CPython gives 2 for the first call and 0 for the second.
#
# Expected UART output, which is what CPython prints for the same program:
#   len-true 1
#   len-false 0
#   bool-field 1
#   name 1
#   scalar 1
#   poll-first 2
#   poll-second 0
#   END
from pymcu.hal.console import print
from pymcu.hal.uart import UART
from pymcu.types import uint8

USE_COUNTED = True
ticks: uint8 = 0


class Bag:
    def __init__(self, n: uint8) -> None:
        self._n = n

    def __len__(self) -> uint8:
        return self._n


class Flag:
    def __init__(self, v: uint8) -> None:
        self._v = v

    def __bool__(self) -> uint8:
        return self._v


class Counted:
    def __init__(self, unused: uint8) -> None:
        self._unused = unused

    def __len__(self) -> uint8:
        global ticks
        ticks += 1
        if ticks > 2:
            return 1
        return 0


class Full:
    def __init__(self) -> None:
        self._bag = Bag(3)

    def truth(self) -> uint8:
        if self._bag:
            return 1
        return 0


class Empty:
    def __init__(self) -> None:
        self._bag = Bag(0)

    def truth(self) -> uint8:
        if self._bag:
            return 1
        return 0


class Switch:
    def __init__(self) -> None:
        self._flag = Flag(1)

    def truth(self) -> uint8:
        if self._flag:
            return 1
        return 0


class Scalar:
    # The control: a field holding a NUMBER is tested as the number it is.
    def __init__(self) -> None:
        self._n = 5

    def truth(self) -> uint8:
        if self._n:
            return 1
        return 0


class Poller:
    # The field is assigned inside a branch, which is where a driver assigns it.
    def __init__(self) -> None:
        if USE_COUNTED:
            self._src = Counted(0)
        else:
            self._src = Bag(1)

    def poll(self) -> uint8:
        n: uint8 = 0
        while not self._src:
            n += 1
            if n > 20:
                return 99
        return n


bag = Bag(3)
f = Full()
e = Empty()
s = Switch()
c = Scalar()
p = Poller()


def main() -> None:
    uart = UART(9600)
    print("len-true", f.truth())
    print("len-false", e.truth())
    print("bool-field", s.truth())
    if bag:
        print("name", 1)
    else:
        print("name", 0)
    print("scalar", c.truth())
    print("poll-first", p.poll())
    print("poll-second", p.poll())
    print("END")
    while True:
        pass
