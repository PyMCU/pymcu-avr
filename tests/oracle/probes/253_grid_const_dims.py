# expect: match
# doc: docs/language/limitations.md:693
from pymcu.types import const

W: const[int] = 4
H = 3

g = [[5] * W for _ in range(H)]
g[1][1] = 7
for row in g:
    s = 0
    for x in row:
        s = s + x
    print(s)
print(len(g))
print("END")
