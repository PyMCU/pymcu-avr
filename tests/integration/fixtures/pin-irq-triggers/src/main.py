# PyMCU -- pin-irq-triggers: which EDGE each Pin.irq trigger actually fires on.
#
# Pin.IRQ_HIGH_LEVEL used to fall off the end of pin_irq_setup's if/elif chain,
# leaving EICRA at its reset value -- low level -- with EIMSK enabled anyway. The
# reset value being itself a valid mode is what made the bug invisible: nothing
# stated which edge each trigger was supposed to select, so selecting the wrong
# one looked exactly like selecting the right one.
#
# So this fixture states it, on the two external interrupts the chip has:
#
#   INT0 on PD2, Pin.IRQ_RISING -> GPIOR1 counts LOW->HIGH transitions only
#   INT1 on PD3, Pin.IRQ_CHANGE -> GPIOR2 counts BOTH transitions
#
# Pin.IRQ_CHANGE is the constant this fixture also exists for: trigger 3 was
# always implemented and always the default, and could be named only by writing
# `Pin.IRQ_FALLING | Pin.IRQ_RISING`, which is 3 by arithmetic.
#
# Data-space addresses (ATmega328P): GPIOR1 = 0x4A, GPIOR2 = 0x4B
#
from pymcu.chips.atmega328p import GPIOR1, GPIOR2
from pymcu.hal.gpio import Pin
from pymcu.types import uint8


def on_rise():
    GPIOR1.value = GPIOR1.value + 1


def on_change():
    GPIOR2.value = GPIOR2.value + 1


def main():
    GPIOR1.value = 0
    GPIOR2.value = 0

    rise = Pin("PD2", Pin.IN)
    edge = Pin("PD3", Pin.IN)

    rise.irq(Pin.IRQ_RISING, on_rise)
    edge.irq(Pin.IRQ_CHANGE, on_change)

    while True:
        pass
