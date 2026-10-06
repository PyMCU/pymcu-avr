# expect: match
# doc: https://docs.pymcu.org/limitations/#exception-handling
# An unbound handler whose nested raise is FRESH (`raise ValueError(...)` writes
# its own record) never observes the caught record, so keeping its snapshot was
# dead weight -- 46 bytes that pushed an unmodified CircuitPython fixture past
# the flash limit. The fresh raise still propagates its own record: the outer
# handler must answer 9, the fresh argument, not the caught 5.
try:
    try:
        raise OSError(5)
    except OSError:
        try:
            raise ValueError(2)
        except ValueError:
            raise OSError(9)
except OSError as e:
    print(e.args[0])
print("END")
