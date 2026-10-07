# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# Codex review of PyMCU golperf's proven-power-of-two mod rewrite (case 3):
# the loop prepass invalidates a called method's written fields in every
# constant-tracking table except the proven-range one. `set4()` sets
# self.w = 4 on the first pass; `5 % o.w` must answer 1 from the second
# iteration on, not keep the first iteration's `& 31` baked in forever.
from pymcu.types import uint8, uint16


class Box:
    def __init__(self):
        self.w: uint16 = 32

    def set4(self):
        self.w = 4


o = Box()
i: uint8 = 0
while i < 2:
    print(5 % o.w)
    o.set4()
    i = i + 1
print("END")
