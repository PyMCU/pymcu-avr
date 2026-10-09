# millis-no-init: millis() must arm its own timebase.
#
# This program never calls millis_init() -- the whole point of the test. Before
# the fix, a program that called millis()/micros()/monotonic*() without calling
# millis_init() itself read a timer that nothing ever started: Timer0's overflow
# interrupt was never enabled, so millis() stayed frozen at 0 forever, silently.
# millis()'s own docstring raises on every OTHER architecture specifically to
# avoid this ("a counter frozen at 0 turns every test into one that never
# fires"); AVR alone had the silent freeze instead of that raise.
#
# The compiler now reports [NEEDS_TIMEBASE] for a resolved millis() call the
# same way it already did for micros()/ticks_ms()/monotonic(), so the build
# driver injects the millis_init() preamble (Timer0 OVF @ prescaler 64) with
# no millis_init() call anywhere in this source.
#
# Expected: after a real delay, millis() reads a nonzero, realistic elapsed
# count instead of 0.
from pymcu.types import uint8, uint32, asm
from pymcu.chips.atmega328p import GPIOR0, GPIOR1
from pymcu.time import millis, delay_ms


def main():
    delay_ms(20)
    t: uint32 = millis()
    GPIOR0.value = uint8(t & 0xFF)
    GPIOR1.value = uint8((t >> 8) & 0xFF)
    asm("BREAK")
    while True:
        pass
