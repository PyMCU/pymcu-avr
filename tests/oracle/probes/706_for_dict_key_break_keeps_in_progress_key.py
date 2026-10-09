# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# `for k in d:` over a compile-time dict unrolls one iteration per key, folding the loop
# variable at compile time only, with no backing store, regardless of break/return/raise.
# A read after the loop saw whatever the name held BEFORE the loop (stale), not the key
# the break's own iteration was processing. Integer keys sidestep a separate,
# pre-existing, out-of-scope gap: a string key's IDENTITY does not survive past the
# loop, only a one-character key's byte value does.
d = {1: 10, 2: 20, 3: 30}
for k in d:
    break
print(k)
print("END")
