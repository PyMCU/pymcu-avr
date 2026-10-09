# expect: compile
# doc: https://docs.pymcu.org/roadmap/
# The descriptor's numeric facts -- ram_size / flash_size / eeprom_size --
# fold through the same binding.
from pymcu.chips import __CHIP__

if __CHIP__.ram_size >= 2048:
    x = 1
if __CHIP__.flash_size >= 32768:
    y = 1
if __CHIP__.eeprom_size >= 1024:
    z = 1
