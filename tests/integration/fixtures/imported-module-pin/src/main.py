from pymcu.hal.uart import UART
from leds import blink

uart = UART(115200)
uart.println("IMP")
blink()
uart.println("END")

while True:
    pass
