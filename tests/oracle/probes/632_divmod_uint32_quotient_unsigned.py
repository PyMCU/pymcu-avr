# expect: match
# doc: https://docs.pymcu.org/limitations/#integers
# divmod(0xFFFFFFFF, 1) == (4294967295, 0): the quotient's int32 pattern is -1,
# so it must carry the Unsigned mark or the read folds it back to -1.
q, r = divmod(0xFFFFFFFF, 1)
print(q)
print(r)
