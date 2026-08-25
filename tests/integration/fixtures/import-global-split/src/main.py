from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8
from countmod import counter, bump


def main():
    print("IG")
    seed: uint8 = GPIOR0.value
    if seed > 10:
        bump()
    print(counter)

    while True:
        pass
