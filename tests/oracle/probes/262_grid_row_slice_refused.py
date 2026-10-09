# expect: match
# doc: https://docs.pymcu.org/limitations/#iterators-and-comprehensions
# `s = g[0][1:3]` is a row slice: the binding path makes a real element copy
# (a Python slice is a fresh list, never a view), so this compiles and prints 2.
g = [[0] * 4 for _ in range(2)]
s = g[0][1:3]
print(len(s))
