# expect: refuse 'e.errno' is the error code of an OSError
# doc: docs/language/limitations.md:286
# A handler spelling that can catch something that is not an OSError has no
# error code to read -- the compiler refuses instead of inventing one.
try:
    raise ValueError(5)
except ValueError as e:
    print(e.errno)
