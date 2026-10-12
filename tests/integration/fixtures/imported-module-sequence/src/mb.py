_K = b"\xA5\x3C"
_M = b"\x55"


class Dev:
    def __init__(self, n: int) -> None:
        self._n = n

    def pick(self) -> int:
        b = _K
        return b[self._n]

    def single(self) -> int:
        b = _M
        return b[self._n]
