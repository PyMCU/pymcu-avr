# async-create-task: create_task() builds a compile-time task set.
#
# Each create_task call site gets its own global coroutine instance and a running
# flag; asyncio.run() becomes a round-robin over exactly those, plus the main
# coroutine. The two tasks have DIFFERENT periods on purpose: a round-robin polls
# a task that is not due yet, and the interleaving is what proves each one keeps
# its own deadline rather than sharing the scheduler's.
#
# fast(): F every 10 ms, six times  -> 10, 20, 30, 40, 50, 60 ms
# slow(): S every 25 ms, twice      -> 25, 50 ms
# main(): waits 80 ms, then prints DONE and returns, which ends run()
#
# Expected UART output:
#   CT
#   F F S F F S F F   (one marker per line, in time order)
#   DONE
import asyncio
from pymcu.types import uint32
from pymcu.hal.uart import UART


async def fast():
    i: uint32 = 0
    while i < 6:
        await asyncio.sleep_ms(10)
        print("F")
        i = i + 1


async def slow():
    k: uint32 = 0
    while k < 2:
        await asyncio.sleep_ms(25)
        print("S")
        k = k + 1


async def main():
    asyncio.create_task(fast())
    asyncio.create_task(slow())
    await asyncio.sleep_ms(80)
    print("DONE")


uart = UART(115200)
uart.println("CT")
asyncio.run(main())

while True:
    pass
