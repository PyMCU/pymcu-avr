# A module-level object driven from a function that is NOT main (PyMCU issue #159).
# The construction is injected into main's body, so main has to be lowered before the
# functions that read the instance; defined above main, they used to fail the build on a
# bit index the program does not write.
from pymcu.hal.gpio import Pin
from pymcu.time import delay_ms

led = Pin("PD5", Pin.OUT)


def turn_on():
    led.high()


def turn_off():
    led.low()


def main():
    turn_on()
    delay_ms(50)
    turn_off()

    while True:
        pass
