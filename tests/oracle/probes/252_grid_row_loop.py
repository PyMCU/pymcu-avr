# expect: match
# doc: docs/language/limitations.md:673
g = [[0] * 4 for _ in range(3)]
n = 0
for row in g:
    for x in range(4):
        row[x] = n
        n = n + 1
for i, row in enumerate(g):
    print(i, row[0], row[3])
for x in g[2]:
    print(x)
print("END")
