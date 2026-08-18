# zca-field-method-call: a user class calling a method ON ONE OF ITS FIELDS.
#
# A method whose body reaches through a field (self._pin.value()) cannot be
# compiled once and shared: the shared body would take the field as a number,
# and a number has no methods. It used to be outlined anyway, and the whole
# program failed to build with "call to undefined function 'self__pin_value'"
# -- even when nothing ever called the method.
#
# Expected UART output:
#   on=1
#   off=0
from pymcu.types import uint8
from pymcu.hal.gpio import Pin
from pymcu.hal.uart import UART
from pymcu.time import delay_ms


class Holder:
    def __init__(self):
        self._pin = Pin("PB5", Pin.OUT)

    def get(self) -> uint8:
        return self._pin.value()

    def on(self):
        self._pin.high()

    def off(self):
        self._pin.low()


def main():
    uart = UART(9600)
    h = Holder()

    h.on()
    delay_ms(1)
    uart.write_str("on=")
    uart.print_byte(h.get())

    h.off()
    delay_ms(1)
    uart.write_str("off=")
    uart.print_byte(h.get())

    while True:
        pass
