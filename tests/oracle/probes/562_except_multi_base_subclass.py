# expect: match
# doc: https://docs.pymcu.org/limitations/#exception-handling
# class F(ValueError, OSError) raised where `except OSError` waits: the base
# scan stopped at the first RESOLVABLE base, so ValueError (outside the OSError
# subtree) ended it and no edge was recorded -- F(5) fell through to unhandled.
# The scan now keeps looking until a base inside the subtree records the edge.
class F(ValueError, OSError):
    pass

try:
    raise F(5)
except OSError:
    print("caught")

print("end")
print("END")
