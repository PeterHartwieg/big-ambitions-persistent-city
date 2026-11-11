def edit(p, pairs):
    s = open(p, encoding='utf-8', newline='').read()
    crlf = '\r\n' in s
    s = s.replace('\r\n', '\n')
    for a, b in pairs:
        assert a in s, (p, a[:90])
        s = s.replace(a, b, 1)
    open(p, 'w', encoding='utf-8', newline='').write(s.replace('\n', '\r\n') if crlf else s)
