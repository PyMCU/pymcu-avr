# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# `for c in "ab":` unrolls one iteration per character, folding the loop variable at
# compile time only -- no backing store at all, regardless of break/return/raise. A read
# after the loop always saw whatever the name held BEFORE the loop (stale), not the
# character the break/raise's own iteration was processing, nor the last character on a
# guaranteed-complete loop. ord() sidesteps a separate, pre-existing, out-of-scope gap
# (a single-character string's IDENTITY does not survive past the loop, only its byte
# value does -- print(c) right here would show a plain number, not CPython's character).
for c in "ab":
    break
print(ord(c))
print("END")
