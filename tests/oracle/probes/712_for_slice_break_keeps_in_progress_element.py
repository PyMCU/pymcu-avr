# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# `for v in buf[lo:hi]:` unrolls over the slice's indices. A bytearray's elements are
# real SRAM loads, already copied for real on every iteration regardless of break, so
# this exact shape never had the bug the sibling compile-time-sequence forms did --
# kept as a regression guard for the loopStack/materialize/settle machinery this form
# shares with them (the one shape that COULD fold, a non-SRAM-tracked array, cannot be
# declared in this language in practice -- every fixed array ends up SRAM-tracked).
buf = bytearray([1, 2, 3, 4, 5])
for v in buf[1:4]:
    break
print(v)
print("END")
