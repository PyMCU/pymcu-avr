# A module-level float keeps the value it was written with (PyMCU#379).
#
# The scan typed a module-level binding from an INTEGER evaluation of its initializer,
# and that evaluation answers for a float literal by truncating it: 0.1 gave 0 and 1.5
# gave 1. The name was then an integer, the module-level store folded to that integer,
# and every read came back as a small int, so `timeout * 1000000.0` printed 0.0 while
# the same expression written as a literal printed 100000.0. Nothing was said.
#
# Four spellings and two modules here, with an int global and a literal as controls.
#
# Expected UART output, which is what CPython prints for the same program:
#   timeout 100000.0
#   TIMEOUT 100000.0
#   literal 100000.0
#   annotated 0.25
#   negative -2.5
#   count 7
#   cfg.scale 0.25
#   cfg.GAIN 1.5
#   cfg.steps 9
#   END
from pymcu.hal.console import print
from pymcu.hal.uart import UART
import cfg

timeout = 0.1
TIMEOUT = 0.1
annotated: float = 0.25
negative = -2.5
count = 7


def main() -> None:
    uart = UART(9600)
    print("timeout", timeout * 1000000.0)
    print("TIMEOUT", TIMEOUT * 1000000.0)
    print("literal", 0.1 * 1000000.0)
    print("annotated", annotated)
    print("negative", negative)
    print("count", count)
    print("cfg.scale", cfg.scale)
    print("cfg.GAIN", cfg.GAIN)
    print("cfg.steps", cfg.steps)
    print("END")
    while True:
        pass
