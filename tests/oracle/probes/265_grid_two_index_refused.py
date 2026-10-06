# expect: refuse a subscript with more than one index
# doc: https://docs.pymcu.org/limitations/#iterators-and-comprehensions
g = [[0] * 4 for _ in range(2)]
print(g[1, 2])
