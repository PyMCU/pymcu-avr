# expect: match
# doc: docs/language/limitations.md:698
g = [[0] * 4 for _ in range(3)]
for x in range(4):
    g[1][x] = x + 10
r = g[1]
print(len(r))
print(r[0])
print(r[3])
r[2] = 99
print(g[1][2])
s = 0
for v in r:
    s = s + v
print(s)
print("END")
