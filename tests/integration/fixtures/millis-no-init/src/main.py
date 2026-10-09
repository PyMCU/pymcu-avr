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
#
# GPIOR2 is claimed and zeroed too: millis() reads _millis_fract (ISR-shared,
# single byte) on every call, which makes AvrGpiorPromotion eligible to
# promote it onto the one free GPIOR in the optimized build only (GPIOR0/1
# are already claimed above). Its value at BREAK would then be the ISR's
# cycle-exact fractional carry -- legitimately a few units different between
# an optimized and an unoptimized build that reach the same delay_ms(20) exit
# a few cycles apart -- which the differential harness's generic snapshot
# otherwise compares unconditionally. Claiming GPIOR2 here (the promotion
# pass skips any GPIOR the program already references) keeps this fixture's
# own checkpoint -- GPIOR0/GPIOR1, millis()'s actual value, the thing this
# fixture exists to assert on -- under comparison.
from pymcu.types import uint8, uint32, asm
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2
from pymcu.time import millis, delay_ms


def main():
    delay_ms(20)
    t: uint32 = millis()
    GPIOR0.value = uint8(t & 0xFF)
    GPIOR1.value = uint8((t >> 8) & 0xFF)
    GPIOR2.value = 0
    asm("BREAK")
    while True:
        pass
