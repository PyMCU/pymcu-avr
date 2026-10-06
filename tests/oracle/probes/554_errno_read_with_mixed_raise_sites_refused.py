# expect: refuse reads the raise's integer argument
# doc: docs/language/limitations.md:286
# Every raise this handler can catch must carry an integer for e.errno to have
# a single answer; a string-message raise in the same try means there is no
# integer on some path, so the read is refused.
from pymcu.chips.atmega328p import GPIOR0

try:
    if GPIOR0.value:
        raise OSError(5)
    else:
        raise OSError("malo")
except OSError as e:
    print(e.errno)
