# PyMCU -- global-array-store: `cfg[i] = v` inside a function must write the
# module-level array, whether or not a `global` statement names it (#460).
#
# `cfg = bytearray(4)` at module level registered two spellings: `main.cfg`
# (what main's own init and reads emitted) and `cfg` (the scan's record). A
# store inside a function resolved to the bare `cfg`, a second dead slot --
# the write vanished and `print(cfg[0])` still read 0.
#
# In Python an element store needs no `global` declaration (only rebinding
# the name does), so both spellings of the function are covered.
#
# Expected UART output:
#   42
#   7
#   49
#   done
from pymcu.hal.console import print
from pymcu.hal.uart import UART

cfg = bytearray(4)


def poke():
    global cfg
    cfg[0] = 42


def poke_no_decl():
    cfg[1] = 7


def bump():
    cfg[0] += 7


uart = UART(9600)

poke()
poke_no_decl()
print(cfg[0])
print(cfg[1])

bump()
print(cfg[0])

print("done")

while True:
    pass
