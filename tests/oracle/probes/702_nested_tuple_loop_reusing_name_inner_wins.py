# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# Both loops here unroll with no break, so each only used to leave a compile-time-only
# fold of its own last element, no real physical write. A read after both loops resolved
# to the OUTER loop's own unrelated real storage by default, answering with the outer's
# last element (2) instead of the inner's (8) -- the inner loop's own fold never committed
# a real write the way a guaranteed-complete loop's read-after case now does, so the
# write that actually ran LAST at runtime (the inner loop's) was invisible to the read.
for v in (1, 2):
    for v in (7, 8):
        pass
print(v)
print("END")
