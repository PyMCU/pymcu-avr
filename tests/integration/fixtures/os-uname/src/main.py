# os-uname: PyMCU/PyMCU#466.
#
# os.uname() is a compile-time five-field record of __CHIP__, not an
# operating system. Adafruit DHT writes `"Linux" not in uname()`;
# platformdetect writes `"RP2350" in uname().machine`.
#
# WHAT DISCRIMINATES:
#   1  -- "Linux" not in uname()
#   1  -- "RP2350" not in uname().machine (this fixture is atmega328p)
#   1  -- "atmega328p" in uname().machine
# A compile that still refused `import os` would not build.
from os import uname
from pymcu.time import delay_ms


def main():
    while True:
        print(1 if "Linux" not in uname() else 0)
        print(1 if "RP2350" not in uname().machine else 0)
        print(1 if "atmega328p" in uname().machine else 0)
        print("END")
        delay_ms(1200)
