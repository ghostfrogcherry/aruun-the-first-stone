# 0003: Server-owned world and the edit journal

Status: accepted. Date: 2026-11-16.

## Decision
- The world belongs to the server; characters belong to players. Single player runs on an
  `OfflineMultiplayerPeer` that is its own server, so there is one code path.
- Clients send requests (`request_dig`, `request_place`, `request_blast`, `request_learn`,
  `request_speak`). The server validates reach, block rules and word knowledge, converts the
  request into explicit ops (`set`, `fill`, `clear`), applies, journals and broadcasts them.
- Every peer generates terrain from the shared seed; only ops travel over the network.
  A late joiner receives the full journal and applies ops as their chunks load.
- The journal (`user://worlds/<name>/journal.txt`, one op per line) is the source of truth
  for edits and is replayed on load. `chunks.sqlite` (VoxelStreamSQLite) is a host-side cache.
- Destruction: after a carve, the server scans a box around it for pieces with no path to
  the box edge and removes them as `clear` ops with `fx = "fall"`. Each peer spawns its own
  physics debris from that op (cosmetic, not synced).

## Later
- Journal compaction into snapshots; versioned migration when `GENERATION_VERSION` changes.
- Debris that settles back into voxels (server-simulated).
- Per-character saves (known words, inventory) separate from the world.
