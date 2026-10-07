# expect: match
# doc: https://docs.pymcu.org/limitations/#exception-handling
# The C3 check refuses only what cannot linearize: `class F(B, C)` over
# `B(A)` and `C(A)` is a consistent diamond -- B and C each precede A, and
# the base list fixes B before C. The raise is caught by `except A`, which
# both routes descend from.
class A(OSError):
    pass

class B(A):
    pass

class C(A):
    pass

class F(B, C):
    pass

try:
    raise F(5)
except A:
    print("caught")
print("END")
