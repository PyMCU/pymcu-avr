# expect: match
# doc: docs/language/limitations.md:983
xs = []
xs.extend([1, 2, 3])
print(xs[0], xs[1], xs[2], len(xs))
print("END")
