# Whole-surface probe for adafruit_ds18x20 + adafruit_onewire: every public
# member exercised against a scripted DS18B20 on D2 and an empty bus on D3.
# Under PyMCU this fixture is build-refused -- OneWireBus.crc8 is a
# @staticmethod whose `for byte in data` iterates a bytearray parameter, and a
# shared subroutine's buffer param carries no length, so the loop has no trip
# count (a documented CompileError). The CPython oracle still runs the full
# program so the report records what each member should do.

import board
from adafruit_onewire.bus import OneWireBus, OneWireAddress, OneWireError
from adafruit_onewire.device import OneWireDevice
from adafruit_ds18x20 import DS18X20, RESOLUTION

bus = OneWireBus(board.D2)

# -- OneWireBus: maximum_devices property + validation ------------------------
print("max", bus.maximum_devices)
bus.maximum_devices = 4
print("max", bus.maximum_devices)
try:
    bus.maximum_devices = "x"
except ValueError as e:
    print("VE:", e)
try:
    bus.maximum_devices = 300
except ValueError as e:
    print("VE:", e)

# -- reset: presence on the scripted pin, absent on the empty one -------------
print("present", bus.reset())               # True
print("required", bus.reset(required=True)) # True
bus2 = OneWireBus(board.D3)
print("empty", bus2.reset())                # False
try:
    bus2.reset(required=True)
except OneWireError as e:
    print("OE:", e)

# -- bus-level write/readinto: SKIP_ROM + READ_SCRATCH ------------------------
bus.reset()
bus.write(b"\xcc\xbe")
buf = bytearray(9)
bus.readinto(buf)
print("scratch", buf)

# -- bus.write with a sliced buffer: SKIP_ROM + WRITE_SCRATCH (TH TL CFG) -----
bus.reset()
bus.write(b"\xcc\x4e")
bus.write(bytearray([0x11, 0x22, 0x1f]))    # CONFIG 0x1f -> 9-bit
bus.reset()
bus.write(b"\xcc\xbe")
bus.readinto(buf)
print("cfg9", buf[4])                      # 0x1f = 31
bus.reset()
bus.write(b"\xcc\x4e")
bus.write(bytearray([0x11, 0x22, 0x7f]))    # back to 12-bit for the reads below

# -- scan + OneWireAddress members --------------------------------------------
addrs = bus.scan()
print("n", len(addrs))                      # 1
addr = addrs[0]
print("fam", addr.family_code)             # 0x28 = 40
print("rom", addr.rom)
print("crc", addr.crc)                     # 0xcd = 205
print("ser", addr.serial_number)
print("crc8", OneWireBus.crc8(addr.rom))    # 0 over data+crc
print("crc8i", bus.crc8(addr.rom))         # same through the instance

# -- OneWireDevice: with-block, write, readinto (CRC-checked full read) -------
dev = OneWireDevice(bus, addr)
with dev as d:
    d.write(b"\xbe")
    sbuf = bytearray(9)
    d.readinto(sbuf)
    print("ds", sbuf)
    d.write(b"\x4e")
    d.write(bytearray([0x33, 0x44, 0x3f]))  # 10-bit config via the device
with dev as d2:
    d2.write(b"\xbe")
    d2.readinto(sbuf)
    print("cfg10", sbuf[4])            # 0x3f = 63

# -- DS18X20: temperature, resolution, conversion members ---------------------
ds = DS18X20(bus, addr)
print("res", ds.resolution)                 # 10 (config 0x3f)
ds.resolution = 12
print("res", ds.resolution)                 # 12
try:
    ds.resolution = 7
except ValueError as e:
    print("VE:", e)
print("t", ds.temperature)                  # -10.125
print("delay", ds.start_temperature_read()) # 0.75 for 12-bit
print("rt", ds.read_temperature())          # -10.125
print("addr", ds.address is addr)           # same object
print("afam", ds.address.family_code)   # 0x28 = 40
print("RES", RESOLUTION)                    # (9, 10, 11, 12)

# -- constructor rejects a non-DS18x20 family code ----------------------------
bad = OneWireAddress(bytearray([0x42, 0, 0, 0, 0, 0, 0, 0]))
try:
    DS18X20(bus, bad)
except ValueError as e:
    print("VE:", e)

print("=DONE=")
