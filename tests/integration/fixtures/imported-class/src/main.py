# PyMCU -- imported-class: a class defined in an imported module.
#
# Regression for PyMCU#130. `Sensor(seed)` failed the build with "class 'Sensor' cannot be
# constructed: it has no __init__ method", on a file that defines one. Callee resolution walked
# the module prefixes for FUNCTIONS only, so a class registered under its module's prefix was
# never found and the bare name was used. It failed from the importing file AND from a plain
# function inside the module that defines the class.
#
# GPIOR0 reads 0 out of reset, so base is 10.
#
# Expected UART output:
#   15
#   10
#   11
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.types import uint16
from sensor import Sensor, make


def main():
    seed: uint16 = GPIOR0.value + 10
    s = Sensor(seed)
    print(s.read(5))
    print(s.base)
    print(make(seed))
    print("done")
