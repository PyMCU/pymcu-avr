# One module per device: the pin objects at the top, the functions that drive
# them below. This is the ordinary way to split a program across files.
from pymcu.hal.gpio import Pin, OUTPUT

led = Pin(5, OUTPUT)


def blink():
    led.high()
    led.low()
