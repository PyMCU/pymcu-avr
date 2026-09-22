# expect: match
# doc: docs/language/roadmap.md
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8, uint16, const

def at(s: const[str], i: uint16) -> uint8:
    return ord(s[i])

msg = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaBbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbC"
seed: uint16 = GPIOR0.value
print(at(msg, seed + 257))
print(at(msg, seed + 299))
print("END")
