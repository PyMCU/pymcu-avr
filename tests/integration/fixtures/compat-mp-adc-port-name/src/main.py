# The same program with the port-string form, which already worked.
from machine import ADC, Pin


def main():
    a = ADC("PC0")
    led = Pin(13, Pin.OUT)
    while True:
        if a.read_u16() > 32768:
            led.high()
        else:
            led.low()
