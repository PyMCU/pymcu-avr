# max7219-cs: the MAX7219 must strobe the CS pin its SPI bus was configured with.
#
# The register writes route through a shared subroutine that only takes primitives,
# so it cannot see the bus object; CS is asserted by MAX7219._write_reg at the call
# site via spi.select()/deselect(). Before that fix every write drove the hardware SS
# (PB2) no matter what cs= said, so a MAX7219 wired to PB0 never latched anything.
#
# Bus is SPI(cs="PB0"): PB0 must go low around each 2-byte write and PB2 must stay
# high (it is only configured as an output by spi_init, never toggled).
#
# UART markers (9600 baud) separate the phases so the test can slice the SPI capture:
#   "MX\n"  boot, after SPI init
#   "I\n"   after init()           -- 5 register writes
#   "B\n"   after set_brightness() -- 1 register write
#   "R\n"   after set_row()        -- 1 register write
from pymcu.hal.spi import SPI
from pymcu.hal.uart import UART
from pymcu.drivers.max7219 import MAX7219


def main():
    uart = UART(9600)
    spi = SPI(cs="PB0")
    mx = MAX7219(spi)

    uart.println("MX")

    mx.init()
    uart.println("I")

    mx.set_brightness(3)
    uart.println("B")

    mx.set_row(2, 0x5A)
    uart.println("R")

    while True:
        pass
