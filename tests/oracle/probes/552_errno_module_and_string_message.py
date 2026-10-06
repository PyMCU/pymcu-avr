# expect: match
# doc: docs/language/limitations.md:286
# `import errno` binds the POSIX-stable constants and folds errorcode lookups;
# EIO is 5 on every platform so the raise's argument is the same integer on
# both interpreters. A string raise keeps its message reads unchanged.
import errno

try:
    raise OSError(errno.EIO)
except OSError as e:
    print(e.args[0], len(e.args))

print(errno.errorcode[errno.EIO])
print(errno.errorcode[errno.ENOENT])

try:
    raise OSError("malo")
except OSError as e:
    print(e)
    print(e.args[0])
    print(len(e.args))

print("END")
