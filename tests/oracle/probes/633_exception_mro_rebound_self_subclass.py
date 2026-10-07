# expect: match
# doc: https://docs.pymcu.org/limitations/#exception-handling
# class M(M) under an earlier class M(object): the base resolves to the binding
# the name held before this def, the rebinding CPython accepts -- each class def
# is its own C3 node, so no name resolves to the node being built.
class M(object):
    pass

class M(M):
    pass

class E(Exception, M):
    pass

print("ok")
