# Hooks in this kit

When you copy this **`.cursor/`** tree into another repository, **skills, rules, commands, agents, and contexts** work from that repo’s workspace root without extra configuration.

The **Python files** in this folder were written for **Claude Code–style** hook events (`SessionStart`, `UserPromptSubmit`, `PreToolUse`, …). They read and write **under the project’s `.cursor/` tree** (for example `.cursor/session-data`, `.cursor/contexts`, `.cursor/coding-levels`).

**Cursor** uses **`.cursor/hooks.json`** with different event names and stdin/stdout contracts. Do not assume this Python code runs unchanged in Cursor unless you adapt it and wire `hooks.json` yourself.

## Options

1. **Cursor only** — use skills, rules, commands, agents from `.cursor/`; ignore these Python hooks unless you port them to Cursor’s hook protocol.
2. **Claude Code** — register these scripts in that product’s hook settings; paths in this kit already target `.cursor/` next to your repo root.
