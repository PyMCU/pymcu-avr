# PyMCU -- async-sleep-float: await asyncio.sleep() with a fractional number of seconds.
#
# Regression for PyMCU#109. `await asyncio.sleep(0.5)` never woke: the elapsed microseconds
# were cut to 16 bits before the float compare, so the deadline could not be reached. The root
# cause turned out to be pymcu-avr#7 -- float() of a 32-bit integer read only its low two
# bytes -- and this fixture is what says the await path is right now that it is fixed.
#
# 0.05 s keeps the simulated run short while still crossing the 16-bit microsecond boundary
# (50000 us fits, but the elapsed count passes 65535 well before the second sleep ends).
#
# Expected UART output:
#   one
#   two
#   done
import asyncio
from pymcu.hal.uart import UART


async def job():
    await asyncio.sleep(0.05)
    print("one")
    await asyncio.sleep(0.05)
    print("two")
    print("done")


def main():
    uart = UART(9600)
    asyncio.run(job())
