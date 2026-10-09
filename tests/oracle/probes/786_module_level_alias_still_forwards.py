# expect: match
# doc: https://docs.pymcu.org/limitations/#arithmetic
# A plain local alias of a module-level buffer, outside any function, must forward
# as the buffer itself -- CPython's 10 came back as 0 before the fix: alias compiled,
# was never refused, but had no real storage identity of its own, so head(alias)
# read whatever (uninitialized) bytes alias's own never-written slot happened to hold.
from pymcu.types import uint8


def head(v: bytearray) -> uint8:
    return v[0]


buf = bytearray([10, 20])
alias = buf
print(head(alias))
print("END")
