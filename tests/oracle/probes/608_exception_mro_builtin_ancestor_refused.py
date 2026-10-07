# expect: refuse method resolution order
# doc: https://docs.pymcu.org/limitations/#exception-handling
# class F(ArithmeticError, ZeroDivisionError): ZeroDivisionError really descends
# from ArithmeticError in CPython, so the base order demands the parent ahead of
# its own subclass and no C3 merge exists. The model only knew the OSError
# subtree, so this compiled where CPython raises TypeError.
class F(ArithmeticError, ZeroDivisionError):
    pass

print("ok")
