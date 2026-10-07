# expect: match
# doc: https://docs.pymcu.org/limitations/#compile-time-sequences-in-classes
# `alias = buf` where `buf` is itself a bytearray PARAMETER makes `alias` a buffer under
# another name too -- forwarding it to a callee declared to take the same type is not "one
# element". Nothing propagated that fact for a plain local-to-local copy inside a regular
# function (unlike an @inline parameter binding or a function's array return, which already
# had their own propagation), so `head(alias)` was refused as a scalar reaching a bytearray
# parameter.
def head(v: bytearray) -> int:
    return v[0]


def forward(buf: bytearray) -> int:
    alias = buf
    return head(alias)


print(forward(bytearray([4, 5])))
print("END")
