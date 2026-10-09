# expect: match
# doc: https://docs.pymcu.org/roadmap/
# The same rebind as probe 874, but written inside an `if` at module level rather than
# as a bare top-level statement. ScanGlobals only walks DIRECT top-level statements when
# deciding whether an imported name already has this file's own storage, so a write
# reachable only through a nested block needed its own, fuller check (ProgramWritesName,
# Scan.cs) before the import-seeding loop could tell the rebind apart from an untouched
# import.
from errno import EPERM

cond = True
if cond:
    EPERM = 42
print(EPERM)
