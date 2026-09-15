# PyMCU -- buffer-grown-by-extend: a module-level scratch buffer grown by .extend() at
# construction, which is how every CircuitPython register driver sizes its bus buffer.
#
# PyMCU#362. `.extend()` had no dispatch for a bytearray at all, so the call fell through to
# the message written for an untyped list: "requires a typed list; an untyped '[]' has no
# runtime list", about a construct the program does not contain.
#
# Nothing here is dynamic. Every _fit() is called from a constructor with a literal, so the set
# of sizes the buffer is ever asked for is known while compiling: the buffer takes the LARGEST,
# len() folds to it, and the guard folds away with it. A later, smaller _fit() adds nothing.
#
# The buffer lives in ANOTHER module, which is the case that matters: a buffer declared in an
# imported module is registered under the spelling that module wrote, while the value it lowers
# to carries the qualified one. Keyed off the lowered name, this worked in one file and in no
# library.
#
#   _fit(2), _fit(1), _fit(4)  ->  len(_BUFFER) is 5, the largest asked for
#   the byte at index 4        ->  inside the buffer, and holds what was stored there
#   b.read() with width 2      ->  the byte at index 2
from reg import _BUFFER
from reg.bits import Bits

b = Bits(2)
wide = Bits(4)
narrow = Bits(1)


def main() -> None:
    print(len(_BUFFER))
    _BUFFER[4] = 0x55
    print(_BUFFER[4])
    _BUFFER[2] = 0x33
    print(b.read())
    print("done")
