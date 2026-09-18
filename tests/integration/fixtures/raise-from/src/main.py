# raise-from: PyMCU/PyMCU#434.
#
# `raise X(...) from Y` is accepted and compiled as `raise X(...)`. There is no
# traceback and no __cause__ on this target, so the two are indistinguishable
# once compiled. Y is parsed and discarded -- a syntax error inside it is still
# caught -- the same treatment a non-call raise MESSAGE already gets (#262).
#
# Reduced from adafruit_irremote, which writes `raise _IRRepeatException from None`
# and from the usual `raise TypeError(...) from err` wrapping of a bound handler
# name. Before this, the parser refused the form outright, so this program did
# not build at all.
#
# Covered shapes:
#   a  raise TypeError("wrapped") from err   -- bound exception name after from
#   b  raise TypeError("cleared") from None  -- the irremote spelling with a message
#   c  raise TypeError("plain")              -- the same raise without from, so the
#                                               three messages reaching the handler
#                                               prove the from-clause was discarded
#                                               rather than changing the new type
#
# Expected UART output, which is what CPython prints for the same program:
#   a:wrapped
#   b:cleared
#   c:plain
#   DONE
from pymcu.types import uint8
from pymcu.time import delay_ms


def boom() -> uint8:
    raise ValueError("boom")


def wrap_from_err() -> uint8:
    try:
        return boom()
    except ValueError as err:
        raise TypeError("wrapped") from err


def wrap_from_none() -> uint8:
    try:
        return boom()
    except ValueError:
        raise TypeError("cleared") from None


def wrap_plain() -> uint8:
    try:
        return boom()
    except ValueError:
        raise TypeError("plain")


def main():
    while True:
        try:
            v = wrap_from_err()
            print(f"v={v}")
        except TypeError as e:
            print("a:", end="")
            print(e.args[0])

        try:
            v = wrap_from_none()
            print(f"v={v}")
        except TypeError as e:
            print("b:", end="")
            print(e.args[0])

        try:
            v = wrap_plain()
            print(f"v={v}")
        except TypeError as e:
            print("c:", end="")
            print(e.args[0])

        print("DONE")
        delay_ms(1200)
