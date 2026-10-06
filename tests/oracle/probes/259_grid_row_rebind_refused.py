# expect: refuse not a variable that can be rebound
# doc: https://docs.pymcu.org/limitations/#iterators-and-comprehensions
g = [[0] * 4 for _ in range(2)]
g[1] = [9] * 4
print(g[1][0])
