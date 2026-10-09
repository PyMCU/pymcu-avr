# expect: match
# doc: https://docs.pymcu.org/roadmap/
# A str class attribute, read through an instance and through the class, PASSED to a
# function instead of printed directly -- the argument binder asks the same
# StaticStringOf/TryGetCompileTimeText pair a bare print() does, so the class-level
# fallback has to hold for a call argument too, not only for print()'s own direct
# MemberAccessExpr branch. CPython prints "mine" twice.
class Fake:
    platform = "mine"


def show(p: str) -> None:
    print(p)


f = Fake()
show(f.platform)
show(Fake.platform)
