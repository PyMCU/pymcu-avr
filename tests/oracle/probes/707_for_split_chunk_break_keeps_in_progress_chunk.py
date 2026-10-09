# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# `for chunk in s.split(sep):` unrolls one iteration per piece, folding the loop
# variable at compile time only. A read after the loop always saw whatever the name
# held BEFORE the loop (stale), not the piece the break's own iteration was processing.
# ord() sidesteps the same separate, pre-existing, out-of-scope gap as probe 705: a
# multi-character chunk's IDENTITY does not survive past the loop, only a
# one-character chunk's byte value does.
s = "a,b,c"
for chunk in s.split(","):
    break
print(ord(chunk))
print("END")
