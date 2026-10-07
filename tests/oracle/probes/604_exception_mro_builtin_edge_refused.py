# expect: refuse method resolution order
# doc: https://docs.pymcu.org/limitations/#exception-handling
# The same order contradiction through the one seeded builtin edge:
# TimeoutError is an OSError subclass, so `class F(OSError, TimeoutError)`
# lists a base ahead of its own subclass and CPython raises TypeError at the
# class definition.
class F(OSError, TimeoutError):
    pass

print("ok")
print("END")
