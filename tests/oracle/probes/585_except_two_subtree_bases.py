# expect: match
# doc: https://docs.pymcu.org/limitations/#exception-handling
# `class F(A, B)` where BOTH bases descend from OSError: the base scan recorded
# only the first edge (F -> A), so `except B` never matched a raised F and the
# catch-all answered "wrong". Every in-subtree base now contributes an edge, so
# F descends from A and B alike.
class A(OSError):
    pass

class B(OSError):
    pass

class F(A, B):
    pass

try:
    raise F(5)
except B:
    print("caught")
except Exception:
    print("wrong")

print("end")
print("END")
