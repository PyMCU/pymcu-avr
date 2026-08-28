# dict-iteration: walking a dict literal (PyMCU/PyMCU#200).
#
# A dict binds a compile-time lookup table and no storage, so the loop unrolls over its
# entries and the loop variable is a compile-time constant inside the body, which is what
# makes `codes[k]` fold to that entry's value. The order is insertion order, the same order
# CPython walks, so every line below is the line CPython prints for the same program.
#
# Every number is distinct so no line can be mistaken for another, and the values are read
# through a second lookup rather than printed directly wherever that is what a program would
# do: `codes[k]` is the half that proves the key is a usable constant and not just a number.
#
# Expected UART output:
#   DI
#   1
#   2
#   30
#   7
#   30
#   7
#   1
#   2
#   40
#   2
#   50
#   6
#   7
#   END
from pymcu.hal.uart import UART
from pymcu.types import uint8

uart = UART(115200)
codes = {1: 30, 2: 7}
names = {"red": 40, "green": 2}
letters = {"a": 50, "b": 6}
total: uint8 = 0

print("DI")
for k in codes:
    print(k)
for k2, v in codes.items():
    print(v)
for v2 in codes.values():
    print(v2)
for k3 in codes.keys():
    print(k3)
for n in names:
    print(names[n])
for c in letters:
    print(letters[c])
for k4 in codes:
    if k4 == 1:
        continue
    total = total + codes[k4]
print(total)
print("END")

while True:
    pass
