# machine.ADC(0): the ESP-style channel number MicroPython documents.
# Channel 0 is A0 = PC0 on the Uno, so this must be the same firmware as ADC("PC0").
from machine import ADC, Pin


def main():
    a = ADC(0)
    led = Pin(13, Pin.OUT)
    while True:
        if a.read_u16() > 32768:
            led.high()
        else:
            led.low()
