# ATmega328P: `**kwargs` and `*args` as compile-time mappings and sequences (#368).
#
# A function taking `**kwargs` is specialised per call site, exactly as a function taking a
# ZCA instance already is. At each site the extra keyword arguments are written out in the
# source, so `kwargs` is a closed mapping of literal keys to expressions and every use of it
# folds or unrolls. Nothing is collected and nothing is allocated.
#
#   a  three levels of forwarding: Button -> Debounced -> Sensor, each consuming one named
#      parameter and passing the rest on through super().__init__(pin, **kwargs)
#   b  a default two levels down survives being skipped over (retries is never written)
#   c  the Adafruit compatibility shape: kwargs collected only to be discarded, so that
#      switch_to_output() matches digitalio.DigitalInOut's signature (adafruit_74hc595,
#      adafruit_pcf8574)
#   d  the reads fold: len(kwargs), "k" in kwargs, kwargs["k"], kwargs.get("k", default)
#   e  for k, v in kwargs.items() unrolls
#   f  *args over positions: len(args) and the unrolled for
#
# The same program with the forwarding written out by hand must produce the same firmware.
# Splicing known keys into named parameters is a compile-time rewrite, so there is no
# run-time object anywhere in this file.
#
# Expected UART output (115200 via print):
#   a=7,50,25,9,3 c=1 d=5,10 e=6 f=6,3 g=9
from pymcu.types import uint8
from pymcu.time import delay_ms


class Sensor:
    def __init__(self, pin: uint8, interval_ms: uint8 = 10, retries: uint8 = 3):
        self.pin = pin
        self.interval_ms = interval_ms
        self.retries = retries


class Debounced(Sensor):
    def __init__(self, pin: uint8, short_ms: uint8 = 20, **kwargs):
        self.short_ms = short_ms
        super().__init__(pin, **kwargs)


class Button(Debounced):
    def __init__(self, pin: uint8, long_ms: uint8 = 40, **kwargs):
        self.long_ms = long_ms
        super().__init__(pin, **kwargs)


class ExpanderPin:
    def __init__(self, n: uint8):
        self.n = n
        self.v = 0

    # kwargs here are necessary for signature compatibility with DigitalInOut, which allows
    # specifying pull and other things this class does not have. They are never read.
    def switch_to_output(self, value: uint8 = 0, **kwargs):
        self.v = value


def described(**kwargs) -> uint8:
    n = len(kwargs)
    if "a" in kwargs:
        n = n + kwargs["a"]
    return n + kwargs.get("b", 5)


def summed_keywords(**kwargs) -> uint8:
    s = 0
    for k, v in kwargs.items():
        s = s + v
    return s


def summed_positions(*args) -> uint8:
    s = 0
    for a in args:
        s = s + a
    return s


def len_of_positions(*args) -> uint8:
    return len(args)


def scaled(n: uint8) -> uint8:
    return n + 5


def main():
    # long_ms binds by position, short_ms one level down, interval_ms two levels down,
    # retries is never written and keeps the default it was declared with.
    b = Button(7, 50, short_ms=25, interval_ms=9)

    p = ExpanderPin(3)
    p.switch_to_output(value=1, pull=0)

    # A keyword value that is NOT a literal, computed in the caller and carried two hops.
    # It arrived as 0: the name holding it was written unqualified in the caller's frame and
    # read qualified inside the expansion, which are two different variables and only one of
    # them was ever written. A literal and a bare name both worked, so the shape that failed
    # was the one nothing else in this fixture had.
    m = 4
    g = Button(2, 30, interval_ms=scaled(m))

    while True:
        print(f"a={b.pin},{b.long_ms},{b.short_ms},{b.interval_ms},{b.retries}")
        print(f"c={p.v}")
        print(f"d={described(a=1, b=2)},{described(a=4)}")
        print(f"e={summed_keywords(a=1, b=2, c=3)}")
        print(f"f={summed_positions(1, 2, 3)},{len_of_positions(1, 2, 3)}")
        print(f"g={g.interval_ms}")
        print("DONE")
        delay_ms(1200)
