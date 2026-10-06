# expect: refuse a row of a 2-D grid is not a list -- it has no 'append()' method
# doc: https://docs.pymcu.org/limitations/#iterators-and-comprehensions
g = [[0] * 4 for _ in range(2)]
g[0].append(1)
