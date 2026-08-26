# state-machine-branch-constant: PyMCU/PyMCU#108, with no async in it.
#
# A hand-written state machine. Arm 0 contains a `for` loop and assigns a field a
# constant; arm 1 increments that field. The increment reads the constant arm 0
# assigned, in a branch that cannot be running at the same time, so it folds: the
# field is stored the constant 1 on every call and never accumulates. The machine
# stays in arm 1 for ever and nothing after it is reachable.
#
# Both ingredients are needed. Deleting the `for` from arm 0, or deleting the
# `self._n = 0` and nothing else, makes this print 1 2 3 Z.
#
# The `for` matters because it is what makes poll() flatten into the caller rather
# than be outlined; flattened, the field becomes a plain name the constant folder
# tracks.
#
# This is the shape the coroutine desugar emits for every `await`, which is why an
# `async def` with an await-free `for` loses everything after its first await.
#
# Expected UART output:  F 1 2 3 Z
# Today:                 F 1 1 1 1 ... for ever
from pymcu.types import uint8, uint16, uint32
from pymcu.hal.uart import UART


class M:
    def __init__(self):
        self._state: uint16 = 0
        self._n: uint32 = 0

    def poll(self) -> uint8:
        if self._state == 0:
            for i in range(4):
                pass
            self._n = 0
            self._state = 1
            return 1
        self._n = self._n + 1
        print(self._n)
        if self._n < 3:
            return 1
        return 0


uart = UART(115200)
uart.println("F")
m = M()
r: uint8 = 1
while r == 1:
    r = m.poll()
uart.println("Z")

while True:
    pass
