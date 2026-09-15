from reg import _BUFFER, _fit


class Bits:
    def __init__(self, width: int) -> None:
        self.width = width
        _fit(width)

    def read(self) -> int:
        return _BUFFER[self.width]
