"""Verify the public report links and every evidence byte against its manifest."""
from pathlib import Path
import hashlib,json,re,sys
root=Path(sys.argv[1]) if len(sys.argv)>1 else Path(__file__).parent
files=sorted(p for p in root.rglob('*') if p.is_file())
assert sum(p.stat().st_size for p in files)<5*1024*1024
for p in files:
    assert p.suffix.lower() not in ['.dll','.hsg','.reg','.private','.exe','.zip'],p
    if p.suffix not in ['.png','.gz']:
        t=p.read_text(encoding='utf-8-sig')
        assert not re.search(r'[A-Za-z]:[\\/]+Users[\\/]+',t,re.I),p
        assert not re.search(r'(?:sk-[A-Za-z0-9]{20,}|ghp_[A-Za-z0-9]{20,}|github_pat_[A-Za-z0-9_]{20,})',t),p
        if p.suffix=='.json':json.loads(t)
report=(root/'README.md').read_text(encoding='utf-8')
for link in re.findall(r'\]\(([^)]+)\)',report):
    if '://' not in link:assert (root/link.split('#')[0]).is_file(),link
manifest=json.loads((root/'file-manifest.json').read_text(encoding='utf-8'))
expected={p.relative_to(root).as_posix() for p in files if p.name!='file-manifest.json'}
assert set(manifest)==expected
for rel,entry in manifest.items():
    b=(root/rel).read_bytes()
    assert len(b)==entry['bytes'] and hashlib.sha256(b).hexdigest()==entry['sha256'],rel
print('Validated',len(files),'files;',sum(p.stat().st_size for p in files),'bytes; links, JSON, path exclusions and hashes match.')
