# expect: match
# doc: https://docs.pymcu.org/limitations/#exception-handling
# class E(Exception, N, M) over N(M): the subclass precedes its parent, the base
# list adds no contradiction, and the consistent mix-in ordering compiles.
class M(object):
    pass

class N(M):
    pass

class E(Exception, N, M):
    pass

print("ok")
