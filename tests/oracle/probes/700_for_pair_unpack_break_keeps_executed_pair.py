# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# A `break` inside a pair-unpack tuple/list for-loop ends it at an EARLIER pair than the
# last. CPython leaves BOTH names bound to whatever the break's own iteration bound them
# to, a run-time fact, not the last pair's textual values. The pair-unpack form never
# materialized a real per-iteration write at all (unlike the single-name form, which only
# did so for a literal `break`), so a read after the loop saw whichever pair unrolled
# last with no backing store, 3 and 4, instead of 1 and 2.
for a, b in ((1, 2), (3, 4)):
    break
print(a)
print(b)
print("END")
