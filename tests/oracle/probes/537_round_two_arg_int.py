# expect: match
# doc: docs/language/limitations.md
# round(x, n) on an int keeps CPython's own int semantics: n >= 0 is x unchanged, n < 0
# rounds to a multiple of 10 ** -n, half-to-even, still an int.
print(round(5, 2))
print(round(1234, -2))
print(round(1250, -2))
print(round(1150, -2))
print(round(-1250, -2))
print("END")
