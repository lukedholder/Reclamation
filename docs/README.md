# Reclamation — Design Documentation

Design docs for Reclamation. **Format policy:** hand-authored docs are **Markdown**;
diagrams are **Mermaid** (`.mmd`, rendered to `.png`); the compiled "design bible" PDF is a
**generated artifact**, not hand-edited. Everything lives in the repo, versioned with the code.

> **Restructure in progress.** The layout and links below are the *target*; file migration is
> pending approval. Until then some links resolve to the old top-level paths.

## System design docs

Living, per-system design + status. Each marks **current** (implemented) vs **planned**.

| Doc | Covers | Status |
|---|---|---|
| [Building & Vehicles](systems/building-and-vehicles.md) | constructs, grid, occupancy, placement, physics/anchoring, vehicles, the V2 redesign | current + planned |
| [World Generation](systems/world-generation.md) | voxel planet, chunk streaming, Surface Nets meshing, radial gravity | partial |
| [Combat](systems/combat.md) | auto-turrets, ammo, enemy placeholder | current (basic) |
| [Items & UI](systems/items-and-ui.md) | items, inventory, machine/chest panels, drag-drop | current |
| [Input](systems/input.md) | `GameInput`, input contexts, rebindable bindings | current |

## Structure

```
docs/
  README.md            # this index
  systems/             # living design docs (Markdown = source of truth)
  diagrams/            # Mermaid sources (.mmd) + rendered (.png)
  bible/               # compiled design bible (generated PDF + build script)
  archive/             # superseded docs + legacy Space Crusade corpus
```

## Conventions

- **Filenames:** kebab-case (`building-and-vehicles.md`).
- **Each doc** starts with a title + a status/scope blockquote, then:
  `Overview → Current implementation → Planned / roadmap → Diagrams → Source map`.
- **Status tags:** `current` (coded) · `partial` · `planned`.
- **Diagrams:** author as Mermaid in `diagrams/`; embed the rendered `.png` (or a
  fenced `mermaid` block) in the doc.

## Building the design bible

The bible PDF is **generated** from `systems/*.md` + `diagrams/` — it duplicates nothing:

```
bash docs/bible/build.sh        # -> docs/bible/DesignBible.pdf
```

(Requires pandoc + a LaTeX engine; re-renders Mermaid diagrams if `mmdc` is available.)

## Archive

`archive/` holds superseded docs (e.g. the legacy `BuildingSystem_UML.md`) and the prior
project's source corpus under `archive/space-crusade/` (`.docx` / `.xlsx` / `.txt` + unpacked
XML + images). Reference only — not maintained.
