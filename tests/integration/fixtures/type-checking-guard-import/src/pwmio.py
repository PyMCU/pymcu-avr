# Local pwmio: the try body of the Adafruit TYPE_CHECKING guard resolves this.
from pymcu.types import uint8


class PWMOut:
    def __init__(self) -> None:
        self.n: uint8 = 0
