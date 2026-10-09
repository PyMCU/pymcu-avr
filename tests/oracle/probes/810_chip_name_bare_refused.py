# expect: refuse from pymcu.chips import __CHIP__
# doc: https://docs.pymcu.org/limitations/
# RFC 0014 family 6: the bare `__CHIP__` spelling binds nothing on its own --
# the diagnostic names the import that does.
x = __CHIP__
