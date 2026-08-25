# PyMCU -- print-strings: printing text that is not a literal at the call.
#
# Regression for PyMCU#80 and PyMCU#82, both silent wrong output on a clean build:
#   - `msg = "hello"` then `print(msg)` streamed the flash id as a DECIMAL NUMBER,
#     so the program printed 256 where the text belonged. Only the annotated form
#     (`msg: str = "hello"`) was recorded as a string.
#   - `chr(n)` is the byte itself, which is right internally and wrong for print:
#     print(chr(65)) sent "65" instead of "A".
#
# The seed keeps the run-time chr() case from folding.
#
# Expected UART output:
#   hello
#   A
#   B
#   done
from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8
from pymcu.hal.console import print

seed: uint8 = GPIOR0.value

msg = "hello"
print(msg)

print(chr(65))
print(chr(66 + seed))

print("done")
