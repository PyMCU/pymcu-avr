# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# `for v in reversed(seq):` where seq is a name bound to a constant sequence -- the
# same "break never pushed a loop stack entry" bug as the list-literal form (probe
# 710), plus the same bare-key-vs-qualified-key materialize/settle fix.
seq = [1, 2, 3]
for v in reversed(seq):
    break
print(v)
print("END")
