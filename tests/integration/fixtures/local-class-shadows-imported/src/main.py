# local-class-shadows-imported: adafruit_74hc595's
#   from digitalio import DigitalInOut   (entry file, 1-arg latch)
#   return DigitalInOut(pin, self)       (module-local, 2-arg)
# The module's class owns the name inside get_pin.
#
# WHAT DISCRIMINATES:
#   7  -- get_pin(6).get() is pin+1 on the LOCAL class
from pymcu.time import delay_ms
from digitalio import DigitalInOut
from sr import ShiftRegister


def main():
    while True:
        latch = DigitalInOut(0)
        s = ShiftRegister()
        p = s.get_pin(6)
        print(p.get())
        print("END")
        delay_ms(1200)
