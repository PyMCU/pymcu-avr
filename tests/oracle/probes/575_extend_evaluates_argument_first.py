# expect: match
# doc: docs/language/roadmap.md:63
# CPython evaluates the extend() argument before the receiver mutates: len(xs)
# inside it still answers the size before the bump, so the grown element is 1.
# Lowering the argument after the bump read the grown length and stored 2.
xs = [9]
xs.extend([len(xs)])
print(xs[1])
print("END")
