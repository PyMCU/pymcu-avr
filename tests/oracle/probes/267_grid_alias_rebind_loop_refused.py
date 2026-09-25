# expect: refuse rebinding it inside a loop, branch or handler
# doc: docs/language/limitations.md:698
g = [[0] * 4 for _ in range(3)]
r = g[0]
for y in range(3):
    r = g[y]
    print(r[0])
