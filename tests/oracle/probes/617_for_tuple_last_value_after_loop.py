# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# CPython leaves the loop variable bound to the LAST element once a for-loop
# over a literal tuple/list ends. Before, the cleanup after the unrolled
# loop unconditionally dropped the compile-time binding, exposing an EARLIER
# iteration's leftover storage instead -- wrong even with every element
# constant (`for v in (3, 7): pass` read back garbage, not 7).
state = bytearray([3])
for v in (state[0], 7):
    pass
print(v)

for w in (3, 7):
    pass
print(w)
print("END")
