# expect: match
# doc: https://docs.pymcu.org/roadmap/#language
def fail():
    raise ValueError("boom")
fail()
