# expect: match
# doc: docs/language/limitations.md:698
g = [bytearray(4) for _ in range(3)]
g[0][0] = 9
g[2][3] = 7
for y in range(3):
    s = 0
    for x in range(4):
        s = s + g[y][x]
    print(s)
print("END")
