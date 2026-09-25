# Four I2C writes whose lengths straddle the 8-bit boundary. A counter that is
# a byte wide sends len & 0xFF: 255 arrives whole, 256 sends nothing at all,
# 300 sends 44 and the 513 of an SSD1306 frame sends one. Each buffer carries a
# distinct first byte so the recorder shows which write produced which length.
from machine import I2C

i2c = I2C(0)

short = bytearray(255)
short[0] = 0x11
i2c.writeto(0x3C, short)

exact = bytearray(256)
exact[0] = 0x22
i2c.writeto(0x3C, exact)

over = bytearray(300)
over[0] = 0x33
i2c.writeto(0x3C, over)

frame = bytearray(513)
frame[0] = 0x40
i2c.writeto(0x3C, frame)
