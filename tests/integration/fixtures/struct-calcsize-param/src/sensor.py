from pymcu.types import uint8
from pack import Field


class Dev:
    regs = Field(0x06, "<HH", 16)

    def __init__(self) -> None:
        self.x: uint8 = 0
