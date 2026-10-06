# expect: refuse a 2-D grid cannot be sliced
# doc: https://docs.pymcu.org/limitations/#iterators-and-comprehensions
g = [[0] * 4 for _ in range(3)]
s = g[0:2]
print(len(s))
