# Changelog

## 0.2.0 - 2026-07-11

### Added

- MCP protocol negotiation through version `2025-11-25`, including initialize, initialized notifications, ping, tool listing, and standard tool results.
- Explicit IPC signature adapters and persistent endpoint configuration.
- Event snapshots and change events for GP, target HP, FATEs, nearby enemies, and nearby players.
- Per-category permission controls for actions, movement, chat, and plugin management.
- Release CI with locked dependencies, package verification, and artifact upload.

### Changed

- Nearby-object events now report set differences instead of writing a full snapshot every frame.
- Plugin reload is scheduled without blocking the Dalamud framework thread.
- Automatic port selection remains automatic across restarts.
- Lumina string handling uses the API Level 15 read-only string type.

### Removed

- Misleading `automove_on`, `automove_off`, and unbounded `move_to_target` tools.
- Placeholder `get_job_gauge` tool.
- Event types and configuration options that had no implementation.

### Fixed

- Standard MCP clients can complete the initialization handshake.
- IPC failures no longer report false success.
- Initial login state no longer produces synthetic change events.
- HTTP startup failures no longer prevent the plugin settings UI from loading.
- Commands, UI handlers, pending tasks, and static bridge references are released on unload.
