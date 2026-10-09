# expect: refuse folded as a compile-time constant
# doc: https://docs.pymcu.org/roadmap/
# `errno.EPERM` is folded as a compile-time constant where it is defined (nothing in
# `errno` ever reassigns it): there is no runtime storage at all to seed. This file's
# ONLY rebind of the imported name is a `global EPERM` write inside a function, with no
# matching top-level statement -- the shape probes 874/875 fixed (a bare or `if`-nested
# top-level rebind) never covers, since the defining module's own folding is decided
# before this file's rebind could ever be seen. Before this diagnostic existed,
# print(EPERM) silently kept answering 1 (errno's own value) instead of CPython's 42.
from errno import EPERM


def rebind() -> None:
    global EPERM
    EPERM = 42


rebind()
print(EPERM)
