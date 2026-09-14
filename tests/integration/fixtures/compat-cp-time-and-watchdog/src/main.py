# time.sleep past its old ceiling and under its old floor, the watchdog's mode, and the
# EEPROM size the part actually has (pymcu-circuitpython#26, #19, #16, #25).
#
# sleep() went through a 16-bit millisecond count, so anything past 65.535 s wrapped and
# anything under a millisecond rounded to zero and did not sleep at all. Assigning
# watchdog.mode armed the watchdog whatever the value, and `mode = None` -- upstream's way
# of disabling it -- did not compile. len(nvm) was the ATmega328P's 1024 on every chip.
#
# Read back at the BREAKs:
#   1  GPIOR0/1 = len(nvm)            GPIOR2 = supervisor.runtime.serial_bytes_available
#   2  GPIOR0   = WDTCSR after arming (WDE set means the reset watchdog is running)
#   3  GPIOR0   = WDTCSR after mode = None (0 means off)
#   4  a 500 us sleep has happened between BREAK 3 and here
#   5  a 70 ms sleep has happened between BREAK 4 and here
import microcontroller
import supervisor
import time
from watchdog import WatchDogMode
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2, WDTCSR
from pymcu.types import asm, uint8, uint16


def main():
    n: uint16 = len(microcontroller.nvm)
    GPIOR0.value = uint8(n & 0xFF)
    GPIOR1.value = uint8(n >> 8)
    GPIOR2.value = uint8(supervisor.runtime.serial_bytes_available)
    asm("BREAK")

    microcontroller.watchdog.timeout = 2.0
    microcontroller.watchdog.mode = WatchDogMode.RESET
    microcontroller.watchdog.feed()
    GPIOR0.value = WDTCSR.value
    asm("BREAK")

    microcontroller.watchdog.mode = None
    GPIOR0.value = WDTCSR.value
    asm("BREAK")

    time.sleep(0.0005)
    asm("BREAK")

    time.sleep(0.07)
    asm("BREAK")

    while True:
        pass
