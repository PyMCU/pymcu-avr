# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# `x = buf[0]` proves `x` a scalar element (correctly, at that point); `for x in
# (buf,): pass` then REBINDS `x` to the buffer itself. Without clearing the stale
# proof first, `head(x)` read back the FIRST binding's proof instead of what `x`
# actually holds now, and was refused as a scalar reaching a bytearray parameter --
# CPython returns the element the buffer's own first byte holds.
def head(v: bytearray) -> int:
    return v[0]


def f(buf: bytearray) -> int:
    x = buf[0]
    for x in (buf,):
        pass
    return head(x)


print(f(bytearray([10, 20, 30])))
print("END")
