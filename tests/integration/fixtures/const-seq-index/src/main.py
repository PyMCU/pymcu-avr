# const-seq-index: `seq.index(x)` on a module-level constant tuple, the shape
# adafruit_tcs34725's gain setter writes as `_GAINS.index(val)` over
# `_GAINS = (1, 4, 16, 60)`. A constant needle folds; a run-time needle lowers
# to a first-match compare chain; a miss raises ValueError.
#
# GPIOR0 reads 0 out of reset, so `rt` is a run-time 0 and `rt + 16` is 16.
#
# Expected UART output:
#   2     _GAINS.index(16), folded
#   2     _GAINS.index(rt + 16), compare chain, first match
#   VE    _GAINS.index(rt) misses and the except ValueError catches it
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.hal.console import print
from pymcu.hal.uart import UART
from pymcu.types import uint8

_GAINS = (1, 4, 16, 60)

uart = UART(9600)

print(_GAINS.index(16))

rt: uint8 = GPIOR0.value
print(_GAINS.index(rt + 16))

try:
    _GAINS.index(rt)
except ValueError:
    print("VE")

print("done")

while True:
    pass
