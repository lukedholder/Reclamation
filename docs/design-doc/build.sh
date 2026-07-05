#!/usr/bin/env bash
# Builds the Reclamation Design Document PDF from the living system docs.
#
# The PDF is a GENERATED artifact — never hand-edited. The source of truth is the
# Markdown in docs/systems/ + docs/use-cases.md. Edit those and re-run this.
set -e
cd "$(dirname "$0")/.."   # -> docs/

PANDOC="$(command -v pandoc || true)"
[ -z "$PANDOC" ] && PANDOC="C:/Users/luked/AppData/Local/Programs/Python/Python313/Lib/site-packages/pypandoc/files/pandoc.exe"

OUT="design-doc/reclamation-design-doc.pdf"

# Order defines the chapters. pandoc concatenates the inputs (blank line between each).
"$PANDOC" \
  systems/building-and-vehicles.md \
  systems/world-generation.md \
  systems/combat.md \
  systems/items-and-ui.md \
  systems/input.md \
  use-cases.md \
  -o "$OUT" \
  --pdf-engine=xelatex -H design-doc/header.tex \
  --toc --toc-depth=2 -N --resource-path=.:diagrams \
  -V title="Reclamation — Design and Requirements" \
  -V author="Reclamation" \
  -V geometry:margin=1in -V fontsize=11pt \
  -V colorlinks=true -V linkcolor=blue -V toccolor=blue \
  -V documentclass=report

echo "Built -> docs/$OUT"
