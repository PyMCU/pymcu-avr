from pymcu.types import uint8, uint16
from pymcu.chips.atmega328p import GPIOR0
from pymcu.boards.arduino_uno import LED_BUILTIN
from pymcu.hal.gpio import Pin


def main():
    led = Pin(LED_BUILTIN, Pin.OUT)
    while True:
        seed: uint8 = GPIOR0.value
        total: uint16 = 0
        i: uint8 = 0
        while i < 64:
            total = total + uint16(i) * uint16(seed)
            if (total & 0x0080) != 0:
                total = total - 7
            i = i + 1
        if (total & 0x0100) != 0:
            led.high()
        else:
            led.low()
