# expect: refuse sys
# doc: https://docs.pymcu.org/limitations/
# `sys.platform` without `import sys`: the name is simply not defined -- no
# ambient introspection object exists.
if sys.platform == "atmega328p":
    x = 1
