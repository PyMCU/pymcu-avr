# imported-dotted-super: FrameBuffer lives in the aliased module.
from pymcu.types import uint8


class FrameBuffer:
    def __init__(self, width: uint8):
        self.width: uint8 = width
