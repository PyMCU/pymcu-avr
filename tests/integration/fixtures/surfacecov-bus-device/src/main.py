# test201_surface_bus_device.py -- whole-surface probe for adafruit_bus_device.
# Members exercised (surface.tsv): I2CDevice.__init__ (i2c, device_address, the
# probe kwarg both ways plus a probe that fails), .i2c / .device_address reads,
# readinto / write / write_then_readinto with and without the start/end window
# kwargs, and __enter__/__exit__ through `with`. SPIDevice.__init__ (spi,
# chip_select given and defaulted, cs_active_value, baudrate, polarity, phase,
# extra_clocks), every stored attribute read, and __enter__/__exit__ through
# `with` -- including the extra_clocks tail write on exit.
#
# fixture.json ACKs I2C address 0x40 and attaches a scripted SPI slave.
# readscript.txt feeds every read in order: I2C reads consume their in-bytes,
# SPI reads the same queue. SPI writes consume script bytes on the sim only, so
# every read is sequenced before the first SPI write.
import board
import busio
import digitalio

from adafruit_bus_device.i2c_device import I2CDevice
from adafruit_bus_device.spi_device import SPIDevice

i2c = busio.I2C(board.SCL, board.SDA)

# probe=True (the default): __probe_for_device writes b"" and the 0x40 slave ACKs.
dev = I2CDevice(i2c, 0x40)
print(dev.device_address)

# probe=False: no transaction at all, so a dark address constructs cleanly.
dev_np = I2CDevice(i2c, 0x50, probe=False)
print(dev_np.device_address)

# probe against a missing address: writeto NACKs, the read fallback NACKs too,
# and __init__ reports ValueError("No I2C device at address: 0x60").
try:
    I2CDevice(i2c, 0x60)
except ValueError as e:
    print(e)

# readinto(buf): three scripted bytes fill the whole buffer.
buf = bytearray(3)
with dev as bus:
    bus.readinto(buf)
print(buf[0], buf[1], buf[2])

# readinto(buf, start=1, end=3): only the window receives bytes.
buf2 = bytearray(5)
with dev as bus:
    bus.readinto(buf2, start=1, end=3)
print(buf2[0], buf2[1], buf2[2], buf2[3], buf2[4])

# write(buf) and write(buf, start=1, end=2): bytes go on the wire, not to us.
out = bytearray([0x0E, 0xAB])
with dev as bus:
    bus.write(out)
    bus.write(out, start=1, end=2)

# write_then_readinto with no window kwargs: the is-None default arms run.
ans = bytearray(2)
with dev as bus:
    bus.write_then_readinto(bytearray([0x10]), ans)
print(ans[0], ans[1])

# write_then_readinto with all four window kwargs.
cmd = bytearray([0x10, 0x11, 0x12])
ans2 = bytearray(4)
with dev as bus:
    bus.write_then_readinto(cmd, ans2, out_start=1, out_end=3, in_start=1, in_end=3)
print(ans2[0], ans2[1], ans2[2], ans2[3])

print(dev.i2c is not None)

# SPIDevice: every init kwarg lands on a stored attribute.
spi = busio.SPI(board.SCK, board.MOSI, board.MISO)
cs = digitalio.DigitalInOut(board.D10)
sd = SPIDevice(spi, cs, baudrate=250000, polarity=0, phase=1, extra_clocks=9)
print(sd.baudrate)
print(sd.polarity)
print(sd.phase)
print(sd.extra_clocks)
print(sd.cs_active_value == False)
print(sd.chip_select is not None)
print(sd.spi is not None)

# __enter__ hands back the bus object; __exit__ writes ceil(9/8)=2 clock bytes.
with sd as s:
    r = bytearray(2)
    s.readinto(r)
    print(r[0], r[1])
    s.write(bytearray([0xAA]))

# chip_select defaulted to None: no pin is driven, on enter or on exit.
sd2 = SPIDevice(spi)
print(sd2.chip_select is None)
print(sd2.baudrate)
with sd2 as s:
    s.write(bytearray([0x55]))

print("=DONE=")
