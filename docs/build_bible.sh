#!/usr/bin/env bash
# Rebuilds docs/DesignBible.pdf from DesignBible.md + diagrams/*.mmd.
#
# Diagrams are pre-rendered to PNG and committed, so the PDF rebuild works even
# without the Mermaid renderer. If mmdc is available it re-renders first.
set -e
cd "$(dirname "$0")"

# --- locate tools -----------------------------------------------------------
PANDOC="$(command -v pandoc || true)"
if [ -z "$PANDOC" ]; then
  PANDOC="C:/Users/luked/AppData/Local/Programs/Python/Python313/Lib/site-packages/pypandoc/files/pandoc.exe"
fi
MMDC_TOOL="/c/Users/luked/AppData/Local/Temp/mmdc-tool"
MMDC="$MMDC_TOOL/node_modules/.bin/mmdc"

# --- 1. (optional) re-render diagrams --------------------------------------
if [ -x "$MMDC" ] || command -v mmdc >/dev/null 2>&1; then
  [ -x "$MMDC" ] || MMDC="mmdc"
  echo "Rendering diagrams..."
  for f in diagrams/*.mmd; do
    "$MMDC" -i "$f" -o "${f%.mmd}.png" -b white -s 3 \
      ${MMDC_TOOL:+-p "$MMDC_TOOL/puppeteer-config.json"} >/dev/null 2>&1 || \
      "$MMDC" -i "$f" -o "${f%.mmd}.png" -b white -s 3
  done
else
  echo "mmdc not found - using committed PNGs."
fi

# --- 2. build the PDF -------------------------------------------------------
echo "Building DesignBible.pdf..."
"$PANDOC" DesignBible.md -o DesignBible.pdf \
  --pdf-engine=pdflatex -H bible_header.tex \
  --toc --toc-depth=2 -N \
  -V geometry:margin=1in -V fontsize=11pt \
  -V colorlinks=true -V linkcolor=blue -V toccolor=blue \
  -V documentclass=report
echo "Done -> docs/DesignBible.pdf"
