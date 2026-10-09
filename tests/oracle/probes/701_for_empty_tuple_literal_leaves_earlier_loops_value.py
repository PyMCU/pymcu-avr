# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# A zero-trip tuple/list literal (`for v in (): pass`) never binds its target, the same as
# an empty range (probe 662's own case, for range specifically). A guaranteed-complete
# tuple/list loop used to leave only a compile-time-only fold with no backing store, so a
# later empty loop reusing the same bare name read whatever a DIFFERENT, unrelated real
# write had left, not the earlier loop's own last element.
for v in (1, 2):
    pass
for v in ():
    pass
print(v)
print("END")
