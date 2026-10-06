# expect: match
# doc: https://docs.pymcu.org/limitations/#iterators-and-comprehensions
xs = []
xs.extend([1, 2, 3])
print(xs[0], xs[1], xs[2], len(xs))
print("END")
