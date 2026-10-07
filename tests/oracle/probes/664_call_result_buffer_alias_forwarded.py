# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# A name bound from a call that simply returns its own bytearray parameter is a buffer
# under another name too. Same root cause as 663: the refusal used to answer on the
# ABSENCE of proof that a name is a buffer, and a call result the compiler never
# modeled as a buffer forward (no CopyArrayIdentity/BindArrayAlias fires for a plain
# bytearray-typed return) fell through that default and was refused, even through an
# @inline identity function.
def head(v: bytearray) -> int:
    return v[0]


def ident(v: bytearray) -> bytearray:
    return v


def forward(buf: bytearray) -> int:
    returned = ident(buf)
    return head(returned)


print(forward(bytearray([4, 5])))
print("END")
