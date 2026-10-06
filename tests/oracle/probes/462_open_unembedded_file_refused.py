# expect: refuse open('x.txt'): no file of that name is embedded
# doc: https://docs.pymcu.org/limitations/#built-ins-summary
# open() is supported only over a file the driver embedded; a name nothing embedded is refused
# and the diagnostic names the file.
from pymcu.chips.atmega328p import GPIOR0

s = GPIOR0.value

f = open("x.txt")
print(1)
print("END")
