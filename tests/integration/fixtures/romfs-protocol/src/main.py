# The whole romfs protocol (RFC 0008) over one 13-byte file: open in a `with`
# (auto-embedded: no files= entry), read(n) folded while the position is a
# compile-time value, readline(max), readinto, seek in all three whences, tell,
# os.stat[6], os.listdir -- then a run-time-position read so the flash-load path
# is exercised too. Each print is one line the test compares.
import os
import struct
import pymcu.chips.atmega328p as chip

with open("data.txt", "rb") as f:
    hdr = f.read(5)
    print(hdr[0])          # 80  'P'
    print(hdr[4])          # 85  'U'
    print(f.tell())        # 5
    line = f.readline(16)
    print(line[0])         # 10  '\n' -- read(5) stopped AT it, so the line is "\n"
    print(len(line))       # 1
    print(f.tell())        # 6   -- pos just past the newline, as CPython's
    f.seek(0, 2)
    print(len(f.read(1)))  # 0   -- read() past the end is empty, not an error

f = open("data.txt", "rb")
buf = bytearray(5)
print(f.readinto(buf))     # 5
print(buf[4])              # 85  'U'
f.seek(0)
print(f.tell())            # 0
pair = f.read(2)
print(pair[0])             # 80  'P'
print(pair[1])             # 121 'y'
v = struct.unpack("<B", f.read(1))[0]
print(v)                   # 77  'M'
f.seek(-4, 2)
print(f.read(1)[0])        # 111 'o'  (byte 9 of 13)
print(os.stat("data.txt")[6])  # 13
for name in os.listdir():
    print(name)            # data.txt
f.seek(chip.PINB[0])       # run-time position: reads become flash loads
print(len(f.read(2)) >= 0)
f.close()
print("END")


def main():
    while True:
        pass
