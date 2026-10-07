# expect: refuse not defined
# doc: https://docs.pymcu.org/limitations/#exception-handling
# class E(Exception, Missing): the deferred definedness check the ordinary-class
# path already runs covers exception bases too -- CPython raises NameError.
class E(Exception, Missing):
    pass

print("unreachable")
