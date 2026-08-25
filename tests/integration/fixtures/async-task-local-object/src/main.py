# async-task-local-object: an object created inside a coroutine belongs to that
# coroutine, one copy per instance of it.
#
# Two instances of the same coroutine each build their own Acc and add to it. In
# CPython they report 11 and 21. See the test for what PyMCU reports today and why.
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
