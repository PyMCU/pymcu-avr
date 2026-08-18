# zca-method-calls-mutator: a method that calls a sibling method which mutates
# self must keep the write.
#
# W1: bump() -> inc(), where inc() writes self.n. The write used to land on a
#     copy of the field and vanish, so the counter never moved.
# W2: bump() assigns from a sibling that only READS -- the case that always
#     worked, kept here as the control.
# W3: two inc() calls in the same method, so a second expansion of the same
#     mutator has to see the first one's value.
#
# Expected UART output:
#   ZCA
#   w1=5 w2=5 w3=5
from pymcu.types import uint8
from pymcu.hal.uart import UART


class W1:
    n: uint8

    def __init__(self, s: uint8):
        self.n = s

    def bump(self):
        self.inc()

    def inc(self):
        self.n = self.n + 1

    def value(self) -> uint8:
        return self.n


class W2:
    n: uint8

    def __init__(self, s: uint8):
        self.n = s

    def bump(self):
        self.n = self.next_val()

    def next_val(self) -> uint8:
        return self.n + 1

    def value(self) -> uint8:
        return self.n


class W3:
    n: uint8

    def __init__(self, s: uint8):
        self.n = s

    def bump2(self):
        self.inc()
        self.inc()

    def inc(self):
        self.n = self.n + 1

    def value(self) -> uint8:
        return self.n


def main():
    uart = UART(9600)
    uart.println("ZCA")

    a = W1(3)
    a.bump()
    a.bump()

    b = W2(3)
    b.bump()
    b.bump()

    c = W3(3)
    c.bump2()

    print(f"w1={a.value()} w2={b.value()} w3={c.value()}")

    while True:
        pass
