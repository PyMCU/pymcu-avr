# expect: refuse method resolution order
# doc: https://docs.pymcu.org/limitations/#exception-handling
# class E(Exception, M, N) over N(M): a fresh pseudo-node per base occurrence hid
# the parent/subclass edge between the mix-ins, so the order contradiction
# compiled. CPython raises TypeError at the class definition.
class M(object):
    pass

class N(M):
    pass

class E(Exception, M, N):
    pass

print("ok")
