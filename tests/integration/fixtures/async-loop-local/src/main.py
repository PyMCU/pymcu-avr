# PyMCU -- async-loop-local: a local accumulated in a for loop and read after an await.
#
# Regression for PyMCU#115. A local that has to survive an await is promoted to a field of the
# generated state machine and every reference is rewritten to self.<name>. The rewriter passed
# an await-free `for` through whole, so the loop body kept writing the plain local while the
# rest of the coroutine read the field: two storage locations for one name, and the total came
# out 0. The widths disagreed as well, the field uint16 and the loop's local uint8.
#
# The coroutine is driven by hand rather than through asyncio.run(), because run() with a
# for-range and no await inside is PyMCU#108 and the program never reaches the print. Swap it
# back to `asyncio.run(job())` once #108 is closed.
#
# Expected UART output:
#   6
#   done
import asyncio
from pymcu.types import uint8, uint16
from pymcu.hal.uart import UART


async def job():
    total: uint16 = 0
    for i in range(4):
        total = total + i
    await asyncio.sleep_ms(1)
    print(total)
    print("done")


def main():
    uart = UART(9600)
    t = job()
    r: uint8 = t.poll()
    while r == 1:
        r = t.poll()
