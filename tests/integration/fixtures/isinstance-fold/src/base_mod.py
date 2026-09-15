from pymcu.types import uint8


class MCP3xxx:
    def __init__(self, chan_count: uint8) -> None:
        self._chan_count: uint8 = chan_count
