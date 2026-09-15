# Half of the PyMCU/PyMCU#420 reproduction: the subclass, in a DIFFERENT file, with no
# __init__ of its own -- reduced from adafruit_mcp3xxx/mcp3008.py, which subclasses
# MCP3xxx (defined in adafruit_mcp3xxx/mcp3xxx.py) the same way.
from base_mod import Base


class Sub(Base):
    pass
