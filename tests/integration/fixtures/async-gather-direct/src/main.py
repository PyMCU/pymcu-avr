# async-gather-direct: the coroutines are called straight into gather().
#
# `asyncio.gather(fast(), slow())` is the form CPython users write. Each call
# builds a state machine, and gather() drives both -- no intermediate names.
# asyncio.run(coro()) is the same shape with one coroutine.
#
# fast(): prints F every 10 ms, six times  -> ~10, 20, 30, 40, 50, 60 ms
# slow(): prints S every 35 ms, twice      -> ~35, 70 ms
#
# Expected UART output:
#   ASY
#   F F F S F F F S   (one marker per line)
#   R R              (asyncio.run drives one more pass of two markers)
#   T:OK             (gather took between 60 and 150 ms of wall clock)
import asyncio
from pymcu.types import uint32
from pymcu.hal.uart import UART
from pymcu.time import millis


async def fast():
    i: uint32 = 0
    while i < 6:
        await asyncio.sleep_ms(10)
        print("F")
        i = i + 1


async def slow():
    k: uint32 = 0
    while k < 2:
        await asyncio.sleep_ms(35)
        print("S")
        k = k + 1


async def twice():
    n: uint32 = 0
    while n < 2:
        await asyncio.sleep_ms(5)
        print("R")
        n = n + 1


def main():
    uart = UART(115200)
    uart.println("ASY")

    t0: uint32 = millis()
    asyncio.gather(fast(), slow())
    asyncio.run(twice())
    elapsed: uint32 = millis() - t0

    if elapsed >= 60:
        if elapsed < 150:
            print("T:OK")

    while True:
        pass
