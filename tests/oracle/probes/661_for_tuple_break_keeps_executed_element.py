# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# A `break` can end an unrolled tuple/list for-loop at an EARLIER iteration than the last.
# CPython leaves the loop variable bound to whichever element the break's OWN iteration was
# processing, a run-time fact, not the tuple's textual last element. Folding the post-loop
# read to the LAST element unconditionally (case 3's own fix, applied too broadly) answered
# `for v in (1, 2): break; print(v)` with 2 instead of 1.
for v in (1, 2):
    break
else:
    print(99)
print(v)
print("END")
