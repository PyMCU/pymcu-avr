# expect: match
# doc: docs/language/limitations.md:785
"""Fixed at #436: chr() keeps its character across a return.

The caller used to print 66, the code point, because the only site that ever
wrote a character recognised the chr() CALL by its syntax and a return hides it.
"""
def make(n: int) -> int:
    return chr(n)
print(make(66))
print("END")
