# Module-level mutable global with a non-zero initializer must hold that value at
# startup. It used to land in BSS (zeroed by crt0) with the initializer dropped,
# so it silently read 0; the fix injects the init into main().
#
# A second global is mutated at runtime to prove the value is a real RAM cell,
# not a folded compile-time constant.
#   "3007\n"  -- _seed initialized to 3007
#   "3008\n"  -- _seed + 1 after a runtime increment
from pymcu.types import uint16
from pymcu.hal.uart import UART

_seed: uint16 = 3007


def main():
    uart = UART(9600)
    print(_seed)
    bump()
    print(_seed)
    while True:
        pass


def bump():
    global _seed
    _seed = _seed + 1
