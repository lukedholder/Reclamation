# Reclamation — Design Documentation

Design docs for Reclamation. **Format policy:** hand-authored docs are **Markdown**;
diagrams are **Mermaid** (`.mmd`, rendered to `.png`); the compiled Reclamation Design
Document PDF is a **generated artifact**, not hand-edited. Everything lives in the repo,
versioned with the code.

## System design docs

Living, per-system design + status. Each marks **current** (implemented) vs **planned**.

| Doc | Covers | Status |
|---|---|---|
| [Building & Vehicles](systems/building-and-vehicles.md) | constructs, grid, occupancy, placement, physics/anchoring, vehicles, the V2 redesign | current + planned |
| [World Generation](systems/world-generation.md) | voxel planet, chunk streaming, Surface Nets meshing, radial gravity | partial |
| [Combat](systems/combat.md) | auto-turrets, ammo, enemy placeholder | current (basic) |
| [Items & UI](systems/items-and-ui.md) | items, inventory, machine/chest panels, drag-drop | current |
| [Input](systems/input.md) | `GameInput`, input contexts, rebindable bindings; planned build/combat modes | current + planned |
| [Use Cases](use-cases.md) | gameplay fantasies + control-flow scenarios | reference |

## Structure

```
docs/
  README.md            # this index
  use-cases.md         # gameplay fantasies + control-flow scenarios
  systems/             # living design docs (Markdown = source of truth)
  diagrams/            # Mermaid sources (.mmd) + rendered (.png)
  design-doc/          # compiled Reclamation Design Document (generated PDF + build)
  archive/             # superseded docs + legacy Space Crusade corpus
```

## Conventions

- **Filenames:** kebab-case (`building-and-vehicles.md`).
- **Each doc** starts with a title + a status/scope blockquote, then:
  `Overview → Current implementation → Planned / roadmap → Diagrams → Source map`.
- **Status tags:** `current` (coded) · `partial` · `planned`.
- **Diagrams:** author as Mermaid in `diagrams/`; embed the rendered `.png` (or a
  fenced `mermaid` block) in the doc.

## Building the design document

The Reclamation Design Document PDF is **generated** by compiling `systems/*.md` +
`use-cases.md` — it duplicates nothing:

```
bash docs/design-doc/build.sh    # -> docs/design-doc/reclamation-design-doc.pdf
```

(Requires pandoc + a LaTeX engine.)

## Archive

`archive/` holds superseded docs (e.g. the legacy `BuildingSystem_UML.md`) and the prior
project's source corpus under `archive/space-crusade/` (`.docx` / `.xlsx` / `.txt` + unpacked
XML + images). Reference only — not maintained.
