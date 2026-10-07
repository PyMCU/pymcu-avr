# expect: refuse method resolution order
# doc: https://docs.pymcu.org/limitations/#exception-handling
# class F(A, B) over B(A): the source order demands A before B while B's own
# linearization demands B before A, so no C3 merge exists. CPython raises
# TypeError at the class definition; the edge table only recorded IS-A facts,
# so the class compiled and printed "ok".
class A(OSError):
    pass

class B(A):
    pass

class F(A, B):
    pass

print("ok")
print("END")
