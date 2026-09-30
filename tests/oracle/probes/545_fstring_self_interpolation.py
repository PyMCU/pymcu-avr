# expect: match
# doc: docs/language/limitations.md
# s = f"{s}..." (an f-string that interpolates the name it assigns) used to be refused
# outright -- the naive lowering reuses s's own buffer AND length variable, resetting the
# length to 0 before any part is emitted, so a self-referencing read saw zero bytes and
# silently dropped s's old text. A private temp buffer now snapshots s's current bytes and
# length before either is touched, and every self-referencing part reads the snapshot.
from pymcu.chips.atmega328p import GPIOR0

seed = GPIOR0.value
s = f"n={seed}pad"
print(s)
s = f"{s}"
print(s)
s = f"{s}"
print(s)
print("END")
