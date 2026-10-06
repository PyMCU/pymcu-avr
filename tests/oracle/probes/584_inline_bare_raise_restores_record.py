# expect: match
# doc: https://docs.pymcu.org/limitations/#exception-handling
# A bare `raise` inside an @inline callee lowers inside the calling handler: a
# nested handled raise has overwritten the shared record by then, so the unwind
# must restore that handler's snapshot before re-signalling. Without it the
# outer read answered the inner ValueError's argument (2) for OSError(5).
from pymcu.types import inline

@inline
def rer():
    raise

try:
    try:
        raise OSError(5)
    except OSError:
        try:
            raise ValueError(2)
        except ValueError:
            pass
        rer()
except OSError as e:
    print(e.args[0])
print("END")
