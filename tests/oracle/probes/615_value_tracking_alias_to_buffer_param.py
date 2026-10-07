# expect: refuse reads a whole buffer
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# `one = buf[0]` makes `one` a value-tracking alias of the loaded byte, not a
# buffer alias -- but NameIsScalarAtThisSite treated any presence in
# variableAliases as "not a scalar", so this compiled silently instead of
# refusing: `one` held 10, used as an SRAM address, and the call read back
# garbage (255) instead of ever reaching CPython's TypeError.
def first(buf: bytearray) -> int:
    return buf[0]


buf = bytearray([10, 20, 30])
one = buf[0]
print(first(one))
print("END")
