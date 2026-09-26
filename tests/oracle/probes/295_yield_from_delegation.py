# expect: match
# doc: docs/language/roadmap.md:115
def inner(base):
    k = 0
    while k < 3:
        yield base + k
        k = k + 1
def outer(start):
    yield start
    yield from inner(start * 10)
    yield start + 1
total = 0
for v in outer(2):
    total = total + v
    print(v)
print(total)
seen = 0
for w in outer(5):
    seen = seen + 1
    if w >= 51:
        break
print(seen)
for z in outer(1):
    print(z)
for q in inner(7):
    print(q)
print("END")
