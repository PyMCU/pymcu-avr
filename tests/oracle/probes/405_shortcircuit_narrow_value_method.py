# expect: match
# doc: https://github.com/PyMCU/PyMCU/issues/513
# The value half of 404.
from pymcu.chips.atmega328p import GPIOR0


class Enc:
    def __init__(self):
        self.count = 0

    def scan(self, n):
        last = None
        i = 0
        while i < n:
            pos = (i // 2) * 2 + GPIOR0.value
            changed = last is None or pos != last
            if changed:
                self.count = self.count + 1
                last = pos
            i = i + 1
        return self.count


e = Enc()
print(e.scan(4))
print("END")
