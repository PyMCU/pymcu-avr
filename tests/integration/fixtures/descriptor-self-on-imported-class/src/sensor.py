from pymcu.types import uint8
from pack import Field


class Dev:
    raw = Field(9)

    def __init__(self, base: uint8) -> None:
        self.base = base

    @property
    def volts(self) -> uint8:
        return self.raw * 2
