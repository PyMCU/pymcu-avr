# Three SPI writes whose lengths straddle the 8-bit boundary. A counter that is
# a byte wide sends len & 0xFF: 255 goes whole, 256 sends nothing and 300 sends
# forty-four. The bytes themselves are a ramp so a short transfer is visible in
# what arrives as well as in how much.
from machine import SPI

spi = SPI(0, baudrate=1000000)

short = bytearray(255)
short[0] = 0xA1
spi.write(short)

exact = bytearray(256)
exact[0] = 0xA2
spi.write(exact)

over = bytearray(300)
over[0] = 0xA3
spi.write(over)
