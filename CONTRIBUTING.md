# Contributing

- `main` stays playable. Work on a branch and open a pull request.
- Every world change goes through the server requests in
  `scripts/multiplayer/world_authority.gd`. Never edit voxels directly from player or UI code.
- Content is data: add blocks, words and structures in `content/`, then run
  `python3 tools/content_validator/validate.py`.
- Block ids are permanent. Append new blocks; never renumber.
- Words are provisional until the Avarra language design settles. Don't reuse names
  that already mean something in Aruun canon (the validator checks the obvious ones).
- Record architecture changes as a short ADR in `docs/decisions/`.
- Don't commit world saves or the Voxel Tools binaries. Large art goes through Git LFS.
