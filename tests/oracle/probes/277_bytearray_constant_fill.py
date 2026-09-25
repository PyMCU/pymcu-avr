# expect: match
# doc: docs/language/roadmap.md:60
# A constant-sized bytearray is zero filled. The AVR backend replaces the run of
# identical constant stores by a counted loop, so every size that changes the shape
# of that loop is exercised here: past 256 (16-bit counter), exactly 256 (the 8-bit
# counter wraps out of LDI 0), just under it, and a fill too small to be worth a loop.
big = bytearray(300)
mid = bytearray(256)
small = bytearray(255)
tiny = bytearray(2)

big[0] = 1
big[299] = 2
mid[0] = 3
mid[255] = 4
small[0] = 5
small[254] = 6
tiny[1] = 7

sb = 0
for i in range(300):
    sb = sb + big[i]
print(sb)

sm = 0
for i in range(256):
    sm = sm + mid[i]
print(sm)

ss = 0
for i in range(255):
    ss = ss + small[i]
print(ss)

print(tiny[0], tiny[1])
print(big[150], mid[128], small[100])
print("END")
