# PyMCU -- adc-two-pins: two AnalogPins keep their own channel.
#
# Regression for PyMCU#134. ADMUX is one register shared by every AnalogPin, and it was written
# only in __init__, so with two pins alive the last one constructed owned it and every read
# returned that channel. A two-sensor program -- the most ordinary reason to have an ADC at all
# -- silently read one sensor twice.
#
# The channel register is read back after each start rather than trusted: the bug was
# precisely that the register kept a value the caller did not ask for.
#
# PC0 selects ADMUX 0x40 (AVcc reference, MUX 0) and PC1 selects 0x41.
#
# Expected UART output:
#   64
#   65
#   64
#   done
from pymcu.hal.adc import AnalogPin
from pymcu.hal.console import print
from pymcu.chips.atmega328p import ADMUX
from pymcu.types import uint8


def main():
    a = AnalogPin("PC0")
    b = AnalogPin("PC1")

    # start() rather than read(): read() polls for the conversion to complete, and the
    # emulator finishes one only for a driven channel. Selecting the channel is what this
    # fixture is about, and start() is the same selection on the same register.
    a.start()
    m0: uint8 = ADMUX.value
    b.start()
    m1: uint8 = ADMUX.value
    a.start()
    m2: uint8 = ADMUX.value

    print(m0)
    print(m1)
    print(m2)
    print("done")
