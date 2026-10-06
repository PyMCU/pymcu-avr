# expect: match
# doc: https://docs.pymcu.org/limitations/#iterators-and-comprehensions
g = [[0] * 4 for _ in range(3)]
for y in range(3):
    for x in range(4):
        g[y][x] = y * 4 + x
print(len(g))
print(len(g[0]))
print(len(g[2]))
print(g[0][0])
print(g[1][2])
print(g[2][3])
print("END")
