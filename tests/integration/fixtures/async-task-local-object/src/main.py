# async-task-local-object: PyMCU/PyMCU#116.
#
# An object a coroutine builds for itself belongs to that coroutine, one copy per
# instance of it. A name used ONLY as a method receiver used not to count as used,
# so it was never lifted into the coroutine's state and stayed a static local of
# poll(): two instances of the same coroutine then shared one Acc.
#
# Both workers build their own Acc and add 1 to it, so they report 11 and 21. With
# the object shared they reported 21 and 22, because both constructions ran before
# either awaited and the second overwrote the first.
#
# Expected UART output: TLO 11 21 END
import asyncio
from pymcu.types import uint16
from pymcu.hal.uart import UART


class Acc:
    def __init__(self, start: uint16):
        self.v: uint16 = start

    def add(self, x: uint16):
        self.v = self.v + x

    def get(self) -> uint16:
        return self.v


async def worker(seed: uint16):
    a = Acc(seed)
    await asyncio.sleep_ms(10)
    a.add(1)
    print(a.get())


uart = UART(115200)
uart.println("TLO")
asyncio.gather(worker(10), worker(20))
uart.println("END")

while True:
    pass
