# expect: compile
# doc: https://docs.pymcu.org/roadmap/
# `from pymcu.chips import F_CPU as FC` -- the fact answers under the alias.
from pymcu.chips import F_CPU as FC

if FC == 16000000:
    x = 1
