# async-timebase: asyncio on AVR runs against a real clock.
#
# asyncio.ticks() is the Timer0 micros counter, so `await asyncio.sleep_ms(n)`
# suspends for n real milliseconds instead of returning immediately (or, worse,
# never completing when the counter is frozen at 0). The build driver arms the
# Timer0 overflow ISR automatically because this program has an `async def`.
#
# fast(): prints F every 10 ms, six times  -> ~10, 20, 30, 40, 50, 60 ms
# slow(): prints S every 35 ms, twice      -> ~35, 70 ms
# gather() interleaves them, so S lands between the F markers.
#
# Expected UART output:
#   ASY
#   F F F S F F F S   (one marker per line)
#   T:OK              (gather took between 60 and 150 ms of wall clock)
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


def main():
    uart = UART(115200)
    uart.println("ASY")

    t0: uint32 = millis()
    a = fast()
    b = slow()
    asyncio.gather(a, b)
    elapsed: uint32 = millis() - t0

    # Both coroutines finish together at ~70 ms. A frozen time base would either
    # hang here or fall through in under a millisecond.
    if elapsed >= 60:
        if elapsed < 150:
            print("T:OK")

    while True:
        pass
