# PyMCU -- isr-home-reg-clobber (pymcu-avr#22)
#
# An ISR writes a register from the R2-R15 home pool and the prologue does not push it.
# The pool is callee-saved and the prologue's set was decided before register allocation,
# so whatever mainline code was keeping in that register is destroyed by the first
# interrupt. Nothing is reported.
#
# The allocator hands out one home per NAME, which is not the same as one home per
# function: an @inline expanded in both the handler and main is the SAME name in both,
# so both expansions get the SAME register. That is how it was found on a real Uno, where
# the pulseio capture handler's `now: uint16 = TCNT1.value` sat in R6:R7 and main held a
# live value in R7.
#
# Here `hold` is expanded in both. main's expansion keeps `t` (0xBEEF) in its home across
# a countdown long enough for Timer0 to overflow; the handler's expansion overwrites that
# same register with TCNT1. Before the fix main prints a Timer1... a Timer0 sample instead
# of 0xBEEF, and a different one on every run length. After it, 48879 and 4660.
#
# Timer0 at prescaler 64 overflows every 256 * 64 = 16384 cycles ~= 1.02 ms at 16 MHz,
# so the first overflow lands well inside main's countdown.
from pymcu.types import uint16, interrupt, asm, inline
from pymcu.chips.atmega328p import TCNT1, TCCR0B, TIMSK0, GPIOR0
from pymcu.hal.uart import UART


@inline
def hold(v: uint16, spins: uint16) -> uint16:
    acc: uint16 = v
    i: uint16 = spins
    while i > 0:
        i -= 1
        acc += 1
    return acc


@interrupt(0x0020)
def timer0_ovf_isr():
    # spins = 0, so the handler only writes the two homes and returns.
    GPIOR0.value = hold(TCNT1.value, 0) & 0xFF


def main():
    uart = UART(9600)

    TCCR0B.value = 0x03   # Timer0, prescaler 64
    TIMSK0.value = 0x01   # TOIE0
    asm("SEI")

    low: uint16 = hold(28879, 20000)    # -> 48879 = 0xBEEF
    high: uint16 = hold(4560, 100)      # ->  4660 = 0x1234

    uart.print_uint16(low)    # one line each: print_uint16 ends the line itself
    uart.print_uint16(high)

    while True:
        pass
