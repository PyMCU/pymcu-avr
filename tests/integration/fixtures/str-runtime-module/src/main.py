from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8
import statemod


def main():
    print("SM")
    seed: uint8 = GPIOR0.value
    if seed > 10:
        statemod.bump()
    print(statemod.state)

    while True:
        pass
