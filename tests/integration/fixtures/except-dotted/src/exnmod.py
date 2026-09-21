# except-dotted's module half: the exception classes live HERE so main.py can only
# name them through the module qualifier, the way a CircuitPython library spells its
# own errors (`adafruit_irremote.IRNECRepeatException`).
from pymcu.types import uint8


class DecodeError(Exception):
    pass


class RepeatError(Exception):
    pass


class OtherError(Exception):
    pass


def decode(which: uint8) -> uint8:
    if which == 1:
        raise RepeatError("repeat frame")
    if which == 2:
        raise DecodeError("bad pulse train")
    if which == 3:
        raise OtherError("stray")
    return 42
