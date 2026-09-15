# ATmega328P: `except X as e` binds a bounded exception object (#369).
#
# One exception is live at a time in this model, so the object needs no allocation. The type
# code the dispatcher already compares is one half of it, and the flash address of a
# string-literal message is the other: one store of one word per raise, and only when some
# handler in the program binds a name.
#
#   a  the adafruit_dht simpletest shape: except RuntimeError as error, print(error.args[0])
#   b  print(e) reads the same message
#   d  a bare re-raise from inside a handler that binds a name, caught further out under a
#      second name, so the two bindings cannot share one slot by accident
#   e  a nested try with two bound names live at once
#   f  isinstance(e, X) folds against the code the dispatcher already has, and answers 0 for
#      the type that was not raised rather than being refused as a run-time type test
#
# A DELIBERATE BOUNDARY, exercised in `nested` and not hidden: there is ONE message word
# because there is one live exception, so a handler that catches a second exception loses the
# first one's message. `nested` reads e.args[0] BEFORE the inner try for that reason. Reading
# it afterwards would print the inner message, and the fixture would be asserting something
# that is not the intent.
#
# The expected output below is CPython's, running the same program.
#
# Expected UART output (115200 via print):
#   a:checksum mismatch
#   b:bad value
#   d:inner fault
#   d:outer fault
#   e:index fault
#   c=2
#   f=1,0
#   DONE
from pymcu.types import uint8
from pymcu.time import delay_ms


def checksum() -> uint8:
    raise RuntimeError("checksum mismatch")


def bad_value() -> uint8:
    raise ValueError("bad value")


def relay() -> uint8:
    try:
        return checksum()
    except RuntimeError as inner:
        print("d:inner fault")
        raise


def inner_index() -> uint8:
    raise IndexError("index fault")


def nested() -> uint8:
    try:
        return inner_index()
    except IndexError as e:
        print("e:", end="")
        print(e.args[0])
        try:
            return bad_value()
        except ValueError as e2:
            return 2


def main():
    while True:
        try:
            v = checksum()
            print(f"v={v}")
        except RuntimeError as error:
            print("a:", end="")
            print(error.args[0])

        try:
            v = bad_value()
            print(f"v={v}")
        except ValueError as e:
            print("b:", end="")
            print(e)

        try:
            v = relay()
            print(f"v={v}")
        except RuntimeError as outer:
            print("d:outer fault")

        # Bound to a name first, deliberately. PyMCU streams the literal parts of an
        # f-string before it evaluates a later interpolation, so `print(f"c={nested()}")`
        # would print "c=" before the handler inside nested() printed its own line, and
        # the fixture would be asserting an interleaving rather than an exception.
        c = nested()
        print(f"c={c}")

        try:
            v = bad_value()
            print(f"v={v}")
        except ValueError as e:
            hit = 0
            if isinstance(e, ValueError):
                hit = 1
            miss = 0
            if isinstance(e, KeyError):
                miss = 1
            print(f"f={hit},{miss}")

        print("DONE")
        delay_ms(1200)
