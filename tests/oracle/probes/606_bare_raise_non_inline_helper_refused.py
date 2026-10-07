# expect: refuse bare `raise`
# doc: https://docs.pymcu.org/limitations/#exception-handling
# A bare `raise` re-signals the record of the exception being handled, which a
# handler preserves in a snapshot it reserves. A separately compiled function
# lowers with no handler in scope, so its bare raise propagated whatever record
# a nested handled raise last stored -- the caught OSError(5) came out as
# ValueError(2). Refused: mark the helper @inline so the raise expands at the
# call site.
def rer():
    raise

def clobber():
    try:
        raise ValueError(2)
    except ValueError:
        pass

try:
    try:
        raise OSError(5)
    except OSError:
        clobber()
        rer()
except OSError as e:
    print(e.args[0])
except ValueError as e:
    print("wrong")
    print(e.args[0])
