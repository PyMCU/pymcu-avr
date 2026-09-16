# PyMCU -- property-returns-instance: a @property returning a ZCA instance keeps that
# instance dispatchable (PyMCU#445).
#
# `owner.q.bump()` used to refuse: "'.bump()' cannot be dispatched: its receiver is not a
# name bound to an object, a register, or a value PyMCU defines methods on." `q`'s getter
# body is `return self._q`, exactly as knowable at compile time as reading `owner._q`
# directly -- which already dispatched correctly, before this fix, without a property in
# the way.
#
# The keypad.Keys.events shape this was found from: an EventQueue instance held by a field,
# reached only through a @property so it stays invisible to inspect.getattr_static as a
# writable attribute -- keys.events.get_into(event), here reduced to owner.q.bump().
#
# Expected UART output (CPython: three bumps from zero is 3):
#   1
#   2
#   3
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART


class Queue:
    def __init__(self):
        self._n = 0

    def bump(self):
        self._n = self._n + 1

    def count(self) -> int:
        return self._n


class Owner:
    def __init__(self):
        self._q = Queue()

    @property
    def q(self):
        return self._q


uart = UART(9600)
owner = Owner()
owner.q.bump()
print(owner.q.count())
owner.q.bump()
print(owner.q.count())
owner.q.bump()
print(owner.q.count())
print("done")

while True:
    pass
