#!/bin/bash
set -euo pipefail
HERE="$(cd "$(dirname "$0")" && pwd)"; OUT="$HERE/.."; TMP="$HERE/build"
BRAND="${HART_BRAND:-$HOME/Coding_Projects/youtube/hart-group/brand}"
PY="${HART_PY:-$HOME/Coding_Projects/youtube/hart-group/.venv/bin/python}"
CHROME="/Applications/Google Chrome.app/Contents/MacOS/Google Chrome"
mkdir -p "$TMP/fonts" "$OUT/cards"
cp "$BRAND/fonts/Archivo-variable.ttf" "$BRAND/fonts/IBMPlexMono-Medium.ttf" "$TMP/fonts/"
shot() {
  rm -f "$4"
  for _ in 1 2 3; do
    "$CHROME" --headless=new --disable-gpu --hide-scrollbars --allow-file-access-from-files \
      --force-device-scale-factor=2 --window-size="$2,$3" --virtual-time-budget=5000 \
      --screenshot="$4" "file://$1" >/dev/null 2>&1 || true
    [ -s "$4" ] && return 0
  done
  echo "Chrome could not render $1" >&2; return 1
}
fit() {
  "$PY" -I - "$@" <<'PY'
import io, sys
from PIL import Image
src, w, h, out = sys.argv[1], int(sys.argv[2]), int(sys.argv[3]), sys.argv[4]
im = Image.open(src).convert('RGB')
def enc(img):
    b = io.BytesIO(); img.save(b, 'PNG', optimize=True); return b
sizes = ((w, h), (w * 2 // 3, h * 2 // 3))
for size, quant in [(s, False) for s in sizes] + [(s, True) for s in sizes]:
    img = im.resize(size, Image.Resampling.LANCZOS)
    if quant: img = img.quantize(256, method=Image.Quantize.MEDIANCUT, dither=Image.Dither.FLOYDSTEINBERG)
    b = enc(img)
    if b.tell() < 1000 * 1024:
        open(out, 'wb').write(b.getvalue())
        print(f'{out.split("/")[-1]}: {size[0]}x{size[1]}, {"256 colours" if quant else "rgb"}, {b.tell() // 1024} KB'); sys.exit()
sys.exit(f'{out}: over 1 MB')
PY
}
shot "$HERE/preview.html" 512 512 "$TMP/preview@2x.png"; fit "$TMP/preview@2x.png" 512 512 "$OUT/preview.png"
for f in "$HERE"/cards/*.html; do
  n="$(basename "$f" .html)"
  shot "$f" 1920 1080 "$TMP/$n@2x.png"; fit "$TMP/$n@2x.png" 1920 1080 "$OUT/cards/$n.png"
done
