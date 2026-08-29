# global-rebind-uppercase: a module global written through `global` (PyMCU/PyMCU#220).
#
# `N = 10` at module level is ALL CAPS, and ALL CAPS means "constant" by convention here, which
# is what gives the name no storage so every read folds the initializer. A name this module
# WRITES is not one, whatever it is called: the write produced a `copy` whose DESTINATION was
# the literal 10, so it went nowhere and both reads answered 10.
#
# The same program with a lowercase name has always worked, which is what says this was the
# convention overriding a written statement rather than module globals being unsupported. Both
# spellings are here, so a fix that helped one and not the other cannot pass.
#
# COUNT is the control in the other direction: ALL CAPS and never written, so it must still
# fold to a constant and cost no storage.
#
# Expected UART output:
#   GR
#   10
#   20
#   7
#   14
#   30
#   END
from pymcu.hal.uart import UART
from pymcu.types import uint8

uart = UART(115200)
N = 10
n = 7
COUNT = 30


def bump():
    global N
    N = 20


def bump_lower():
    global n
    n = 14


print("GR")
print(N)
bump()
print(N)
print(n)
bump_lower()
print(n)
print(COUNT)
print("END")

while True:
    pass
