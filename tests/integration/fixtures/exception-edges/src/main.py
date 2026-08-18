# ATmega328P: exception model edges beyond the basic try/except.
#
#   a  bare `raise` inside a handler re-raises to the caller's handler
#   b  `except Exception:` is a catch-all (it used to match nothing: the
#      T-flag model compares codes for equality and Exception had no code,
#      so a ValueError fell through to the unhandled trap)
#   c  user-defined exception classes work (class SensorError(Exception)
#      used to die at link time as an undefined custom_SensorError symbol;
#      they now register as compile-time codes from 32 up)
#   d  subclassing a user exception works (exact-type matching)
#   e  Exception as a later handler after a non-matching specific one
#   f  bare `except:` parses and catches (it used to be a syntax error)
#
# Deliberate boundary, documented: matching is exact-type plus the
# Exception/bare wildcard. `except SensorError:` does NOT catch its
# subclass CalibrationError - Python's full hierarchy walk has no cheap
# translation to a single code compare.
#
# Expected UART output (115200 via print):
#   a=42 b=55 c=77 d=88 e=2 f=66
from pymcu.types import uint8
from pymcu.time import delay_ms


class SensorError(Exception):
    pass


class CalibrationError(SensorError):
    pass


def boom() -> uint8:
    raise ValueError()


def reraiser() -> uint8:
    try:
        return boom()
    except ValueError:
        raise


def custom() -> uint8:
    raise SensorError()


def custom2() -> uint8:
    raise CalibrationError()


def main():
    while True:
        print("BEGIN")
        r = 0
        try:
            r = reraiser()
        except ValueError:
            r = 42
        print(f"a={r}")

        try:
            r = boom()
        except Exception:
            r = 55
        print(f"b={r}")

        try:
            r = custom()
        except SensorError:
            r = 77
        print(f"c={r}")

        try:
            r = custom2()
        except CalibrationError:
            r = 88
        print(f"d={r}")

        try:
            r = custom()
        except ValueError:
            r = 1
        except Exception:
            r = 2
        print(f"e={r}")

        try:
            raise IndexError("bad index")
        except:
            r = 66
        print(f"f={r}")
        print("END")
        delay_ms(1200)
