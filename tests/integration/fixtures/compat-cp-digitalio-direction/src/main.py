# CircuitPython digitalio: `direction = INPUT` keeps the pull the program set, which is
# None unless it said otherwise; `deinit()` releases the pin with no pull (PyMCU#309).
#
# Measured on an Arduino Uno: `value = True` then `direction = Direction.INPUT` left D6 at
# a firm 5 V (the pull-up latch is the output latch on the AVR). The four phases of
# programs/test05_direction_switch.py in cp-digitalio-atmega, without the sleeps, read
# back at breaks (GPIOR1 = DDRD, GPIOR2 = PORTD):
#   1  output True             -> DDRD6 1, PORTD6 1
#   2  direction = INPUT       -> DDRD6 0, PORTD6 0   (floats, no pull)
#   3  pull = UP, then OUTPUT False, then INPUT -> DDRD6 0, PORTD6 1 (the pull asked for)
#   4  deinit()                -> DDRD6 0, PORTD6 0
import board
import digitalio
from pymcu.chips.atmega328p import GPIOR1, GPIOR2, DDRD, PORTD
from pymcu.types import asm


def main():
    pin = digitalio.DigitalInOut(board.D6)
    pin.direction = digitalio.Direction.OUTPUT
    pin.value = True
    GPIOR1.value = DDRD.value
    GPIOR2.value = PORTD.value
    asm("BREAK")

    pin.direction = digitalio.Direction.INPUT
    GPIOR1.value = DDRD.value
    GPIOR2.value = PORTD.value
    asm("BREAK")

    pin.pull = digitalio.Pull.UP
    pin.direction = digitalio.Direction.OUTPUT
    pin.value = False
    pin.direction = digitalio.Direction.INPUT
    GPIOR1.value = DDRD.value
    GPIOR2.value = PORTD.value
    asm("BREAK")

    pin.deinit()
    GPIOR1.value = DDRD.value
    GPIOR2.value = PORTD.value
    asm("BREAK")

    while True:
        pass
