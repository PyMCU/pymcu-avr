# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# A function that returns a tuple from more than one place, where the last `return` is a constant
# and an earlier one is not (`return 7, d - 1` then `return 7, 0`). The caller folded the slot to
# the last return's constant, so every call answered 0. adafruit_onewire's scan loop reads the
# discrepancy it gets back this way.
from typing import Tuple


def search(d: int) -> Tuple[int, int]:
    if d > 1:
        return 7, d - 1
    return 7, 0


def run(d: int) -> int:
    x, y = search(d)
    return y


class Bus:
    def step(self, diff: int) -> Tuple[int, int]:
        if diff > 1:
            return 7, diff - 1
        return 7, 0

    def walk(self) -> int:
        x = 0
        diff = 3
        total = 0
        for _ in range(6):
            x, diff = self.step(diff)
            total = total * 10 + diff
            if diff == 0:
                break
        return total


n = 0
for i in range(3):
    n = n + 1
print(run(n + 2))
print(run(n - 2))
print(Bus().walk())
print("END")
