# expect: refuse not supported
# doc: https://docs.pymcu.org/limitations/#pointer-arithmetic
d = {x: x + 1 for x in [1, 2, 3]}
print(d[1])
print("END")
