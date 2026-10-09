# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# `for i, b in enumerate(buf):` over a fixed array. The index is a compile-time-only
# fold bound under a bare key throughout the whole construct (every in-loop read
# resolves the same way), while a read written AFTER the loop resolves through the
# ordinary qualified name instead -- a DIFFERENT storage slot that nothing ever wrote,
# so it read whatever garbage was already there (255), not the index in progress when
# the break fired.
buf = bytearray([10, 20, 30])
for i, b in enumerate(buf):
    break
print(i)
print(b)
print("END")
