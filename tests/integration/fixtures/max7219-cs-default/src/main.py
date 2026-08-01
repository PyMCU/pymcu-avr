# max7219-cs-default: same program as max7219-cs, but on a bus with no cs= pin.
#
# SPI() leaves chip-select on the hardware SS (PB2), so routing CS through the bus
# object must keep driving PB2 here -- the fix for the configured-cs case must not
# move the default.
from pymcu.hal.spi import SPI
from pymcu.hal.uart import UART
from pymcu.drivers.max7219 import MAX7219


def main():
    uart = UART(9600)
    spi = SPI()
    mx = MAX7219(spi)

    uart.println("MX")

    mx.init()
    uart.println("I")

    mx.set_row(2, 0x5A)
    uart.println("R")

    while True:
        pass
