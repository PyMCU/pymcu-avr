# async-global-then-await: PyMCU/PyMCU#122, the same root as #108 reached through async.
#
# A coroutine that assigns a module-level global used never to wake from its next
# await. The global write changed the shape of the state-0 arm enough to leave a
# stale constant behind, so the arm testing the next state folded away and the
# machine could not advance. Deleting the two `global` lines made the same program
# complete, which is what made this look like a bug about `global`.
#
# Expected UART output: CT 98 99 1 Z
import asyncio
from pymcu.types import uint8
from pymcu.hal.uart import UART

n: uint8 = 0


async def main():
    global n
    n = 1
    print(98)
    await asyncio.sleep_ms(5)
    print(99)


uart = UART(115200)
uart.println("CT")
asyncio.run(main())
print(n)
uart.println("Z")

while True:
    pass
