from inner_mod import Inner
from pymcu.types import uint8


class Manager:
    def __init__(self, v: uint8) -> None:
        self.inner: Inner = Inner(v)

    def __enter__(self) -> Inner:
        return self.inner

    def __exit__(self) -> None:
        pass
