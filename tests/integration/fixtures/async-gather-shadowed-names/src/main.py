# async-gather-shadowed-names: a coroutine may be called `a` or `b`.
#
# asyncio.gather's own parameters are named `a` and `b`, and the async desugar names
# each coroutine's state-machine class after the coroutine. A user coroutine called
# `a` therefore shares its name with the class the parameter is bound to, and the
# call `a.poll()` inside the expansion used to be read as a receiver-less call on the
# class rather than on the instance the parameter holds.
#
# The names here are the point. Renaming them to fast/slow makes the program build
# and run correctly on the unfixed compiler.
#
#   a(): prints A every 10 ms, six times  -> 10, 20, 30, 40, 50, 60 ms
#   b(): prints B every 25 ms, twice      -> 25, 50 ms
#
# Expected UART output:
#   GS
#   A A B A A B A A
#   T:OK        (gather returned after between 55 and 150 ms of wall clock)
import asyncio
from pymcu.types import uint32
from pymcu.hal.uart import UART
from pymcu.time import millis


async def a():
    i: uint32 = 0
    while i < 6:
        await asyncio.sleep_ms(10)
        print("A")
        i = i + 1


async def b():
    k: uint32 = 0
    while k < 2:
        await asyncio.sleep_ms(25)
        print("B")
        k = k + 1


uart = UART(115200)
uart.println("GS")

t0: uint32 = millis()
asyncio.gather(a(), b())
elapsed: uint32 = millis() - t0

if elapsed >= 55:
    if elapsed < 150:
        print("T:OK")

while True:
    pass
