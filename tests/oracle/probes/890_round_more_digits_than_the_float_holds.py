# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
# round(x, n) with more decimals than a float can hold answers x unchanged, as CPython does
# (round(1.5, 20) == 1.5); it used to be refused for n above 15. The counts below straddle the
# digit buffer's 16 bytes, and the exact values keep the float32 and float64 answers identical.
x: float = 1.5
y: float = 2.25
print(round(x, 15))
print(round(x, 16))
print(round(x, 20))
print(round(y, 300))
print(round(y, 2))
print(round(1234.5, -2))
print("END")
