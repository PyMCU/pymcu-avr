# expect: refuse Callable array
# doc: docs/language/limitations.md:317
def double(n):
    return n * 2

def triple(n):
    return n * 3

handlers = [double, triple]

print(handlers[0](4))
print(handlers[1](4))
print("END")
