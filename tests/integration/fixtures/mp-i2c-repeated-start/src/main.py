# MicroPython's write-then-read idiom and the reads that return a buffer, against one
# device at 0x3C (the test attaches it; it answers reads with 0x10, 0x11, ...).
#
#   writeto(addr, buf, False) + readfrom_into -- the second START is a repeated START
#   (TWSR 0x10), which i2c_read_n refused, so the read raised EIO with the device there.
#   readfrom_mem(addr, memaddr, nbytes)         -- upstream's signature; was a 4-arg extension
#   readfrom(addr, nbytes)                      -- was refused as needing a heap
#   writevto(addr, vector)                      -- was missing
#
# The register and the payload come from GPIOR0 (0 at reset), so nothing here folds.
from machine import I2C
from pymcu.chips.atmega328p import GPIOR0

i2c = I2C(0)
rg = bytearray(1)
rg[0] = GPIOR0.value + 0x20
rx = bytearray(3)
i2c.writeto(0x3C, rg, False)
i2c.readfrom_into(0x3C, rx)
print(rx[0], rx[1], rx[2])
got = i2c.readfrom_mem(0x3C, GPIOR0.value + 0x75, 2)
print(got[0], got[1], len(got))
one = i2c.readfrom(0x3C, 1)
print(one[0])
hd = bytearray(1)
hd[0] = GPIOR0.value + 0x40
pay = bytearray(2)
pay[0] = GPIOR0.value + 0xA1
pay[1] = GPIOR0.value + 0xB2
print(i2c.writevto(0x3C, (hd, pay)))
try:
    i2c.readfrom_mem(0x3D, GPIOR0.value, 1)
except OSError:
    print("EIO")
print("END")
