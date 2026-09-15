# The shape adafruit_register/__init__.py is, in eleven lines: a shared scratch buffer that
# starts at one byte and is grown by the constructors that use it.
_BUFFER = bytearray(1)


def _fit(size: int) -> None:
    if len(_BUFFER) < 1 + size:
        _BUFFER.extend(bytes(1 + size - len(_BUFFER)))
