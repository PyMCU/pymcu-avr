# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# `for x, y in zip(a, b):` over two fixed arrays. A bytearray's elements are real SRAM
# loads, already copied for real every iteration, so this exact shape never had the
# bug -- kept as a regression guard. Probe 713 (zip over two NAMED CONSTANT sequences)
# is the one that actually folds with no backing store.
a = bytearray([1, 2, 3])
b = bytearray([4, 5, 6])
for x, y in zip(a, b):
    break
print(x)
print(y)
print("END")
