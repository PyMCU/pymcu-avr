# PyMCU -- sleep-seconds: `from time import sleep` with a float in seconds.
#
# Regression for PyMCU#52. The first line of the first Python program was an
# ImportError telling the user to install a library called `time`.
#
# The claim under test is equivalence, not just that it builds: this firmware must
# be byte-identical to the same blink written with delay_ms(500), which is what the
# accompanying test compares it against.
from pymcu.hal.gpio import Pin
from time import sleep


def main():
    led = Pin("PB5", Pin.OUT)
    while True:
        led.high()
        sleep(0.5)
        led.low()
        sleep(0.5)
