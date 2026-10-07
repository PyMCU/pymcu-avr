# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# A tuple-unpack target bound from a bytearray PARAMETER is a buffer under another name
# too, forwarding it to a callee declared to take the same type is not "one element".
# The refusal used to answer on the ABSENCE of proof that a name is a buffer (any
# registered type, with nothing excluding it), so a tuple-unpack target -- which gets a
# variableTypes entry but none of the alias bookkeeping a plain `alias = buf` copy does
# -- fell through that default and was refused.
def head(v: bytearray) -> int:
    return v[0]


def forward(buf: bytearray) -> int:
    alias, ignored = (buf, 0)
    return head(alias)


print(forward(bytearray([4, 5])))
print("END")
