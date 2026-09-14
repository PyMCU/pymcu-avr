# CircuitPython keypad.Keys: which button changed (pymcu-circuitpython#13).
#
# The module was absent. There is no HAL half: a keypad is digitalio and bookkeeping, and
# the bookkeeping is the same on every architecture.
#
# The queue holds no events of its own. A key's stored state moves only when its change is
# reported, so what is waiting to be read is exactly the set of keys whose pins disagree
# with it: one bit a key instead of a buffer.
#
# The test drives D4, D5 and D6 between the BREAKs and reads back:
#   GPIOR0 = event.key_number, or 255 when there was no event
#   GPIOR1 = event.pressed,    or 255
#   GPIOR2 = how many changes are still waiting
import board
import digitalio
import keypad
from pymcu.chips.atmega328p import GPIOR0, GPIOR1, GPIOR2
from pymcu.types import asm, uint8


def main():
    a = digitalio.DigitalInOut(board.D4)
    b = digitalio.DigitalInOut(board.D5)
    c = digitalio.DigitalInOut(board.D6)
    a.switch_to_input(pull=digitalio.Pull.UP)
    b.switch_to_input(pull=digitalio.Pull.UP)
    c.switch_to_input(pull=digitalio.Pull.UP)

    keys = keypad.Keys([a, b, c], value_when_pressed=False)
    event = keypad.Event()

    GPIOR0.value = keys.key_count
    asm("BREAK")               # the test presses buttons here

    # Three reads, written out: handing the Keys to a helper loses what it is, and the
    # queue is read through it.
    if keys.events.get_into(event):
        GPIOR0.value = event.key_number
        GPIOR1.value = event.pressed
    else:
        GPIOR0.value = 255
        GPIOR1.value = 255
    GPIOR2.value = len(keys.events)
    asm("BREAK")

    if keys.events.get_into(event):
        GPIOR0.value = event.key_number
        GPIOR1.value = event.pressed
    else:
        GPIOR0.value = 255
        GPIOR1.value = 255
    GPIOR2.value = len(keys.events)
    asm("BREAK")

    if keys.events.get_into(event):
        GPIOR0.value = event.key_number
        GPIOR1.value = event.pressed
    else:
        GPIOR0.value = 255
        GPIOR1.value = 255
    GPIOR2.value = len(keys.events)
    asm("BREAK")

    while True:
        pass
