# expect: refuse from pymcu.chips import __CHIP__
# doc: https://docs.pymcu.org/limitations/
# The historic ambient idiom `if __CHIP__.arch == "avr":` no longer folds by
# spelling alone: without the import the name is unbound.
if __CHIP__.arch == "avr":
    x = 1
