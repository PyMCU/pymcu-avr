# PyMCU -- math-pow: runtime `math.pow` on the software float.
#
# pow(x, y) = 2 ** (y * log2(x)), built from the float primitives the image
# already carries: atanh series for log2 after range-reducing into [1, 2),
# Taylor exp for the fractional power of two, exact doubling for the integer
# part. These are the runtime-float exponents adafruit_bmp280's altitude reads
# (`pow(p / sea_level, 0.1903)`) and adafruit_tcs34725's gamma table need --
# the compile-time integer-only builtin could never evaluate them.
#
# UART prints each result scaled and rounded to an integer so the transcript
# is exact.
#
# Expected UART output:
#   A 1024     math.pow(2.0, 10.0)
#   B 225      math.pow(1.5, 2.0) * 100
#   C 3162     math.pow(10.0, 0.5) * 1000  (sqrt(10))
#   D 200      math.pow(0.5, -1.0) * 100
#   E 1000     math.pow(1.001, 0.1903) * 1000  (bmp280 altitude shape, ~1.00019)
#   F 0        math.pow(0.0, 5.0)
#   G 100      math.pow(5.0, 0.0) * 100
#   H 1414     bare pow(2.0, 0.5) * 1000  (sqrt(2), tcs34725's spelling: no math import)
#   I 8        bare pow(2, 3)           (constant integers still fold)
#   END
from pymcu.hal.console import print
import math


def main():
    print("A", int(math.pow(2.0, 10.0) + 0.5))
    print("B", int(math.pow(1.5, 2.0) * 100.0 + 0.5))
    print("C", int(math.pow(10.0, 0.5) * 1000.0 + 0.5))
    print("D", int(math.pow(0.5, -1.0) * 100.0 + 0.5))
    print("E", int(math.pow(1.001, 0.1903) * 1000.0 + 0.5))
    print("F", int(math.pow(0.0, 5.0)))
    print("G", int(math.pow(5.0, 0.0) * 100.0 + 0.5))
    print("H", int(pow(2.0, 0.5) * 1000.0 + 0.5))
    print("I", pow(2, 3))
    print("END")


main()
