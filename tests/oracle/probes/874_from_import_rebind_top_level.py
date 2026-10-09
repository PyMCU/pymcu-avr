# expect: match
# doc: https://docs.pymcu.org/roadmap/
# `from errno import EPERM` then a bare top-level rebind: CPython's EPERM is 42 from
# that assignment on, not errno's own 1. The import-seeding loop (Core.cs) used to copy
# the DEFINING module's value into `globals[sym]` unconditionally, AFTER this file's own
# scan had already folded its own rebind -- the import's copy silently overwrote the
# correct value, so print(EPERM) streamed errno's own 1 instead of this file's 42.
from errno import EPERM

EPERM = 42
print(EPERM)
