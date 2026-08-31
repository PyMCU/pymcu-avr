from pymcu.types import uint8, uint16
from pymcu.chips.atmega328p import GPIOR0
from pymcu.boards.arduino_uno import LED_BUILTIN
from pymcu.hal.gpio import Pin


def main():
    led = Pin(LED_BUILTIN, Pin.OUT)
    while True:
        seed: uint8 = GPIOR0.value
        acc: uint16 = uint16(seed) * 37 + 11
        acc = acc ^ (acc >> 5)
        acc = acc + uint16(seed) * 3
        if (acc & 0x0100) != 0:
            led.high()
        else:
            led.low()
