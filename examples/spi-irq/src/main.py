# ATmega328P: Hardware SPI peripheral -- interrupt-driven byte receive
#
# Demonstrates:
#   - SPI(SPI.PERIPHERAL): configure hardware SPI in peripheral mode
#   - spi.irq(handler): register ISR at SPI STC vector via compile_isr;
#     no @interrupt decorator or manual SPCR/SREG writes needed
#   - ISR<->main handoff through a plain module global: detected as
#     ISR-shared (volatile semantics) and auto-promoted to a GPIOR
#     register, so the received byte moves through single-cycle I/O
#
# Hardware: Arduino Uno as SPI peripheral (connect to any SPI controller)
#   MISO = PB4 (Arduino pin 12) -- data out (peripheral drives this)
#   MOSI = PB3 (Arduino pin 11) -- data in  (controller drives this)
#   SCK  = PB5 (Arduino pin 13) -- clock    (controller drives this)
#   SS   = PB2 (Arduino pin 10) -- chip select (controller drives this)
#   UART TX at 9600 baud
#
# ISR contract: on_byte() MUST read SPDR to clear SPIF; otherwise the
# interrupt re-fires immediately. SPDR holds the byte from the controller.
#
# Note: a received 0x00 byte is indistinguishable from "no data" in this
# simple demo -- the main loop only reports non-zero bytes.
#
# Output:
#   "SPII\n"    -- boot banner
#   "XX\n"      -- two hex digits for each byte received from controller
#
from pymcu.types import uint8
from pymcu.chips.atmega328p import SPDR
from pymcu.hal.spi import SPI
from pymcu.hal.uart import UART

# Last received byte, written by the ISR and consumed by main.
# ISR-shared -> auto-promoted to a GPIOR register; starts at 0 on reset.
rx: uint8 = 0


def on_byte():
    # Reading SPDR clears SPIF and captures the received byte atomically.
    global rx
    rx = SPDR.value


def main():
    uart = UART(9600)
    spi  = SPI(SPI.PERIPHERAL)

    # irq() enables SPIE + SEI and places on_byte at the SPI STC vector.
    # No @interrupt decorator or asm("SEI") needed.
    spi.irq(on_byte)

    uart.println("SPII")

    while True:
        if rx != 0:
            byte: uint8 = rx
            rx = 0
            uart.write_hex(byte)
            uart.write('\n')
