# CircuitPython alarm: waiting on more than one alarm, and knowing which fired
# (pymcu-circuitpython#20).
#
# sleep_until_alarms() took ONE alarm and returned a constant 0, so a program waiting on
# "a time limit or a button" could only wait on one of the two and could not have told them
# apart if it had waited on both. Up to four are polled now and the return value is the
# position of the one that fired.
#
# A TimeAlarm also starts the millisecond time base itself. The build starts that clock only
# for a program that names ticks_ms, monotonic or asyncio, so an alarm was waiting on a
# counter that never moved and sleep_until_alarms never returned.
#
# The test drives D2 high or leaves it low, and reads back:
#   GPIOR0 = which alarm fired (0 = the 50 ms time alarm, 1 = the pin alarm)
import alarm
import board
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import asm, uint8


def main():
    ta = alarm.time.TimeAlarm(monotonic_time=0.05)
    pa = alarm.pin.PinAlarm(board.D2, value=True)

    w: uint8 = alarm.sleep_until_alarms(ta, pa)
    GPIOR0.value = w
    asm("BREAK")

    while True:
        pass
