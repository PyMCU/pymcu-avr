# expect: refuse 'e.errno' is the error code of an OSError
# doc: docs/language/limitations.md:286
# `except (ValueError, OSError)` catches a ValueError too, so e.errno would
# answer a number that raise never meant as an error code -- refused.
try:
    raise OSError(5)
except (ValueError, OSError) as e:
    print(e.errno)
