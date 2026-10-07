# expect: match
# doc: https://docs.pymcu.org/limitations/#exception-handling
# A user class named LookupError (which PyMCU's table does not number) must not
# become IndexError's builtin parent -- the C3 edge resolves to a fixed node.
class LookupError(object):
    pass

class E(LookupError, IndexError, Exception):
    pass

print("ok")
