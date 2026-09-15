# method-factory-returns-class: PyMCU/PyMCU#421.
#
# A method call that returns a class instance -- `led = pcf.get_pin(7)`, an ORDINARY method,
# not a constructor, whose body builds and returns a DigitalInOut -- never tagged the
# assignment target with a class, so the next method call on it (`led.switch_to_output(...)`)
# mangled to an undefined `led_switch_to_output`. Confirmed this is about the receiver's
# missing class, not the keyword argument: the same call fails identically written
# positionally.
#
# Owner/Pin live in pinlib.py, a DIFFERENT file from this one, matching adafruit_pcf8574.py's
# own shape exactly (PCF8574.get_pin() and its DigitalInOut return type are both defined in
# the library module, called from a separate main.py) -- the fix's method-call case alone
# built fine with everything in one file and needed a second, cross-module fix once measured
# against the real library.
#
# Reduced from adafruit_pcf8574.py's PCF8574.get_pin() / DigitalInOut.switch_to_output().
#
# Expected UART output: MF 1 END
import pinlib
from pymcu.types import uint8
from pymcu.hal.uart import UART

uart = UART(115200)
uart.println("MF")

o = pinlib.Owner()
led = o.get_pin(7)
led.switch_to_output(value=True)
print(o.written)

uart.println("END")

while True:
    pass
