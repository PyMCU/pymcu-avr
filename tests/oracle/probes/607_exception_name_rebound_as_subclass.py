# expect: match
# doc: https://docs.pymcu.org/limitations/#exception-handling
# `class A(A)` under an earlier `class A(Exception)`: the base resolves to the
# binding the name held BEFORE this class registered, which is the rebinding
# CPython accepts. Registering the new code first resolved the base to the
# class being defined and refused it as self-inheritance.
class A(Exception):
    pass

class A(A):
    pass

print("ok")
