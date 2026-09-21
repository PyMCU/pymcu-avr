# ATmega328P: `except mod.Exc:` and `except (mod.A, mod.B):` -- the dotted spellings
# the Adafruit libraries use for their own exception classes (irremote simpletest:
# `except adafruit_irremote.IRNECRepeatException:`).
#
#   a  a single dotted type catches the matching code
#   b  a tuple of dotted types shares one handler body across alternatives
#   c  `except (mod.A, mod.B) as e` still binds the message for e.args[0]
#   d  a dotted type does not catch a sibling the tuple did not name (the code
#      propagates to the outer catch-all, not into the wrong handler)
#
# Expected UART output (115200 via print), matching CPython on the same program:
#   a:repeat
#   b: bad pulse train
#   c: repeat frame
#   d:outer
#   DONE
from pymcu.types import uint8
from pymcu.time import delay_ms
import exnmod


def main():
    while True:
        # a: single dotted type
        try:
            v: uint8 = exnmod.decode(1)
            print("a:missed")
        except exnmod.RepeatError:
            print("a:repeat")

        # b: tuple of dotted types, second alternative catches
        try:
            v2: uint8 = exnmod.decode(2)
            print("b:missed")
        except (exnmod.RepeatError, exnmod.DecodeError) as e:
            print("b:", e.args[0])

        # c: same tuple, first alternative catches; bound name reads the message
        try:
            v3: uint8 = exnmod.decode(1)
            print("c:missed")
        except (exnmod.RepeatError, exnmod.DecodeError) as e:
            print("c:", e.args[0])

        # d: a type the tuple did not name must propagate past it
        try:
            try:
                v4: uint8 = exnmod.decode(3)
                print("d:missed")
            except (exnmod.RepeatError, exnmod.DecodeError):
                print("d:wrong handler")
        except Exception:
            print("d:outer")

        print("DONE")
        delay_ms(1000)
