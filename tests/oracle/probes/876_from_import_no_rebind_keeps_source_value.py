# expect: match
# doc: https://docs.pymcu.org/roadmap/
# The regression guard for probes 874/875: an import that is never rebound must keep
# reading the DEFINING module's own value. ProgramWritesName's fuller walk (Scan.cs) must
# answer false here -- EPERM is only ever read in this file -- so the import-seeding
# fix does not also break the ordinary, by-far more common case of a plain import.
from errno import EPERM

print(EPERM)
