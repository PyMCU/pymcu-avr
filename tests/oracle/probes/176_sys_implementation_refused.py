# expect: refuse Module not found: sys
# doc: https://docs.pymcu.org/limitations/ (no interpreter to introspect)
import sys
print(sys.implementation.name)
print("END")
