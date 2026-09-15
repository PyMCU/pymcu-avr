# A MicroPython driver forwards keyword arguments to a layer class (#368).
#
# machine.Signal(pin, invert=0) is the natural base in this layer, and the idiom a user
# driver writes on top of it is the one adafruit_debouncer writes on top of Debouncer: take
# the pin, take whatever else the thing underneath understands, and hand the rest on. The
# driver never names `invert` and never has to.
#
# This is the same compile-time splice as the pure-PyMCU fixture, reached through a layer
# whose __init__ is @inline and takes a ZCA Pin instance, which that fixture does not
# exercise.
#
# The forwarding is by composition, not by inheritance, because `super()` against a class
# imported from a layer is refused today for reasons that have nothing to do with keyword
# arguments: the same driver written `class Led(Signal)` with the forwarding spelled out by
# hand is refused the same way.
#
#   g  invert=1 spliced through: on() drives the pin low and value() reads back 1
#   h  the layer's own default survives a call that passes no keywords at all
#
# Expected UART output (9600):
#   g=1,0
#   h=1,0
#   DONE

from machine import Pin, Signal, UART
from pymcu.types import uint8


class Led:
    def __init__(self, pin_id: uint8, **kwargs):
        self._sig = Signal(Pin(pin_id, Pin.OUT), **kwargs)

    def on(self):
        self._sig.on()

    def off(self):
        self._sig.off()

    def value(self) -> uint8:
        return self._sig.value()


def main():
    uart = UART(0, 9600)

    active_low = Led(13, invert=1)
    plain = Led(12)

    active_low.on()
    on_low: uint8 = active_low.value()
    active_low.off()
    off_low: uint8 = active_low.value()

    plain.on()
    on_plain: uint8 = plain.value()
    plain.off()
    off_plain: uint8 = plain.value()

    uart.println(f"g={on_low},{off_low}")
    uart.println(f"h={on_plain},{off_plain}")
    uart.println("DONE")

    while True:
        pass
