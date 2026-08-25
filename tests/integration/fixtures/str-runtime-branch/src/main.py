from pymcu.chips.atmega328p import GPIOR0
from pymcu.types import uint8

state: str = "idle"


def bump():
    global state
    state = "running"


def main():
    print("SB")
    seed: uint8 = GPIOR0.value

    s: str = "idle"
    if seed > 10:
        s = "running"
    print(s)

    if s == "running":
        print("eq")
    else:
        print("ne")

    c: str = "start"
    i: uint8 = 0
    while i < seed:
        c = "looped"
        i = i + 1
    print(c)

    if seed > 10:
        bump()
    print(state)

    while True:
        pass
