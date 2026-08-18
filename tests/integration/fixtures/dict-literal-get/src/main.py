# dict-literal-get: d.get(key, default) on a compile-time lookup table.
#
# A dict literal is a closed table resolved at compile time, so get() folds for
# a constant key and lowers to the same compare chain as d[key] for a runtime
# one -- with the miss handed the default instead of raising KeyError. The call
# used to mangle into an undefined 'd_get' and fail the build.
#
# Expected UART output:
#   a=20 b=20 c=99
#   d=10 e=99
from pymcu.types import uint8
from pymcu.hal.uart import UART


def main():
    uart = UART(9600)
    d = {1: 10, 2: 20}

    print(f"a={d[2]} b={d.get(2, 99)} c={d.get(5, 99)}")

    k: uint8 = 1
    miss: uint8 = 7
    print(f"d={d.get(k, 99)} e={d.get(miss, 99)}")

    while True:
        pass
