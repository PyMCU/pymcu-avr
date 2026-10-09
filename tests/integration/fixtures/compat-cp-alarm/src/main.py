# CircuitPython alarm: waiting on more than one alarm, and knowing which fired
# (pymcu-circuitpython#20).
#
# light_sleep_until_alarms() took ONE alarm and returned a constant 0, so a program waiting on
# "a time limit or a button" could only wait on one of the two and could not have told them
# apart if it had waited on both. Up to four are polled now and the return value is the
# position of the one that fired.
#
# A TimeAlarm also starts the millisecond time base itself. The build starts that clock only
# for a program that names ticks_ms, monotonic or asyncio, so an alarm was waiting on a
# counter that never moved and light_sleep_until_alarms never returned.
#
# The test drives D2 high or leaves it low, and reads back:
#   GPIOR0 = which alarm fired (0 = the 50 ms time alarm, 1 = the pin alarm)
#
# GPIOR1 and GPIOR2 are claimed and zeroed too: the millisecond time base this
# alarm polls shares an ISR with millis()/micros(), and millis() now reads
# _millis_fract (ISR-shared, single byte) on every call -- making
# AvrGpiorPromotion eligible to promote it onto the one free GPIOR in the
# optimized build only (GPIOR0 is already claimed above). Its value at BREAK
# would then be the ISR's cycle-exact fractional carry, legitimately a few
# units different between an optimized and an unoptimized build whose alarm
# fires a few cycles apart, which the differential harness's generic snapshot
# otherwise compares unconditionally. Claiming GPIOR1 alone still left GPIOR2
# as the one free register for the promotion to land on instead -- measured:
# the differential run then disagreed on GPIOR2 (0x13 vs 0x00), not GPIOR1.
# Claiming BOTH (the promotion pass skips any GPIOR the program already
# references, and atmega328p has only three) leaves it no free register at
# all, so _millis_fract stays an ordinary SRAM byte in both builds and keeps
# this fixture's own checkpoint -- GPIOR0, which alarm fired, the thing this
# fixture exists to assert on -- under comparison.
import alarm
import board
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2
from pymcu.types import asm, uint8


def main():
    ta = alarm.time.TimeAlarm(monotonic_time=0.05)
    pa = alarm.pin.PinAlarm(board.D2, value=True)

    w: uint8 = alarm.light_sleep_until_alarms(ta, pa)
    GPIOR0.value = w
    GPIOR1.value = 0
    GPIOR2.value = 0
    asm("BREAK")

    while True:
        pass
