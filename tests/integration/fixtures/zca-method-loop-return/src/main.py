# PyMCU -- zca-method-loop-return: a single-field instance mutated in a method's loop, and
# the method's value (PyMCU#292)
#
# Three seams met on one program: an unannotated method's return had no result temporary
# (the caller read None); the constant tracked for the field lived under the instance's own
# name and survived the loop (every read folded to the constructor's 3); and `.value` took
# the register-read path, so once the instance had an SRAM slot the writes went there and
# the reads still came from the scalar. A Fader that summed 0..9 answered 3, 10 or 0.
#
# Expected UART (115200):
#   A 3
#   B 13
#   C 15
#   D 25
#   E 28
#   F 7
#   G 45
#   END
from pymcu.hal.console import print


class A:
    def __init__(self):
        self.value = 3

    def plain(self):
        return self.value

    def after_if(self, n):
        if n > 5:
            self.value = self.value + 10
        return self.value

    def after_while(self):
        k = 0
        while k < 2:
            self.value = self.value + 1
            k = k + 1
        return self.value

    def after_loop(self, n):
        for i in range(n):
            self.value = self.value + 1
        return self.value

    def constant_after_while(self):
        k = 0
        while k < 2:
            k = k + 1
        return 7


class Fader:
    def __init__(self):
        self.value = 0

    def up(self, n):
        for i in range(n):
            self.value = self.value + i
        return self.value


def main():
    a = A()
    print("A", a.plain())
    print("B", a.after_if(9))
    print("C", a.after_while())
    print("D", a.after_loop(10))
    print("E", a.after_loop(3))
    print("F", a.constant_after_while())
    f = Fader()
    print("G", f.up(10))
    print("END")

    while True:
        pass


main()
