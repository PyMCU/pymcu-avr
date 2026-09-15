# A compat-layer namespace bound to a local keeps being that namespace (PyMCU#259).
#
# `alarm.pin`, `alarm.time`, `microcontroller.cpu` and `microcontroller.watchdog` are
# module-level singletons; that is how the layers give CircuitPython its dotted spelling.
# Binding one to a name is the ordinary idiom, and what upstream's own documentation shows:
#
#     wd = microcontroller.watchdog
#     while True:
#         wd.feed()
#
# That was filed as a VALUE-TRACKING alias, which is cleared at every label because a copy
# of a scalar may not have run on both paths of a join. A loop emits a label, so by the
# first use inside the loop the name had stopped being an object and the call was refused
# as a method on an integer -- while the same two lines with no loop between them compiled.
# Which object a name stands for does not depend on which path ran.
#
# Expected UART output, which is what CPython prints for the same program:
#   16000000
#   16000000
#   done
import microcontroller
from pymcu.types import uint8

cpu = microcontroller.cpu


def main() -> None:
    wd = microcontroller.watchdog
    wd.timeout = 2
    n: uint8 = 0
    while n < 2:
        print(cpu.frequency)
        wd.feed()
        n += 1
    print("done")
    while True:
        pass
