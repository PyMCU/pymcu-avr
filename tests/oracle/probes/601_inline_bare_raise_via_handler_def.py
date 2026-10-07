# expect: match
# doc: https://docs.pymcu.org/limitations/#exception-handling
# The handler holds no lexical raise at all: the bare `raise` lives in a nested
# @inline it calls, and the record clobber between them comes from an ordinary
# function's handled raise. The snapshot decision used to ask whether the body
# listed a RaiseStmt, so the unwind re-signalled ValueError(2)'s record where
# the caught OSError carried 5.
from pymcu.types import inline

def clobber():
    try:
        raise ValueError(2)
    except ValueError:
        pass

try:
    try:
        raise OSError(5)
    except OSError:
        @inline
        def rer():
            raise

        clobber()
        rer()
except OSError as e:
    print(e.args[0])
print("END")
