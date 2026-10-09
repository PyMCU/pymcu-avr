# expect: compile
# doc: https://docs.pymcu.org/roadmap/
# `import pymcu.chips` licenses the fully-dotted member spelling too.
import pymcu.chips

if pymcu.chips.__FREQ__ == 16000000:
    x = 1
