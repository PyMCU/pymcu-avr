from pymcu.types import uint8


class Inner:
    def __init__(self, v: uint8) -> None:
        self._v: uint8 = v

    def value(self) -> uint8:
        return self._v
