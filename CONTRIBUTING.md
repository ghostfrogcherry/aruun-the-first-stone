# Contributing

- `main` stays playable. Work on a branch and open a pull request.
- Game rules go in `unity/Assets/Aruun/Core` with a test, not in MonoBehaviours. See
  [`unity/README.md`](unity/README.md) for the house rules.
- The world changes only through edit ops (`Aruun.Core.Edits.EditOp`). Never edit voxels
  directly from player or UI code.
- Content is data: add blocks, words and structures in `content/`, then run
  `python3 tools/content_validator/validate.py`.
- Block ids are permanent. Append new blocks; never renumber.
- Words are provisional until the Avarra language design settles. Don't reuse names
  that already mean something in Aruun canon (the validator checks the obvious ones).
- Record architecture changes as a short ADR in `docs/decisions/`.
- Don't commit world saves or Unity's generated folders (Library, Temp, Logs, Builds).
  Commit `.meta` files with their assets. Large art goes through Git LFS.
- Art: one palette, `.vox` files, no code needed. Read `docs/art/README.md`. Small art files are
  exempt from LFS (see `.gitattributes`).
