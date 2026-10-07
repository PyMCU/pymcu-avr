# expect: match
# doc: https://docs.pymcu.org/limitations/#exception-handling
# class A(Exception) then class A(object): the rebinding retires the exception
# binding, and E(Exception, A) merges the ordinary class -- not the dead code.
class A(Exception):
    pass

class A(object):
    pass

class E(Exception, A):
    pass

print("ok")
