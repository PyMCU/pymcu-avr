# expect: match
# doc: https://docs.pymcu.org/limitations/#integers
# divmod(0xFFFFFFFF, 2): the literal is uint32, not -1 -- the fold must run on
# the unsigned value, CPython answers (2147483647, 1).
q, r = divmod(0xFFFFFFFF, 2)
print(q)
print(r)
