# One module per device: the pin objects at the top, the functions that drive
# them below. This is the ordinary way to split a program across files, and it
# is what #117 was about.
from pymcu.hal.gpio import Pin

led = Pin("PD5", Pin.OUT)


def blink():
    led.high()
    led.low()
