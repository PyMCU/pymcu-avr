# expect: refuse inheritance
# doc: https://docs.pymcu.org/limitations/#functions-and-closures
class A:
    pass
class B:
    pass
class C(A, B):
    pass
print("END")
