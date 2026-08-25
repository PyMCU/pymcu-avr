from pymcu.types import uint8

counter: uint8 = 7


def bump():
    global counter
    counter = 42
