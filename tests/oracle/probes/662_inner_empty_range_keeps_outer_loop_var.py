# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# Python's for-loop machinery never binds the target for an empty range -- not even to
# `start` -- so a read afterwards sees whatever the name held before. An inner
# `for v in range(0): pass` reusing the OUTER loop's own bare name used to rebind it to the
# range's start (0) regardless, on every outer iteration, because the compiler forgot the
# outer's existing binding just by entering the inner statement, then wrote the empty
# range's start over it unconditionally when the name was read afterwards.
for v in (1, 2):
    for v in range(0):
        pass
    print(v)
print("END")
