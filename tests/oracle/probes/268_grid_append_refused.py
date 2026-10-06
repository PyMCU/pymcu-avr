# expect: refuse a 2-D grid is a flat fixed array -- it has no 'append()'
# doc: https://docs.pymcu.org/limitations/#iterators-and-comprehensions
g = [[0] * 4 for _ in range(2)]
g.append([0] * 4)
