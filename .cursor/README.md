# Using this `.cursor/` kit

This directory is the **Cursor project kit**: rules, skills, slash commands, agents, and optional hooks. In **cursor-skills** the tree already lives here; in another app it is the same tree at `<workspace>/.cursor/`.

For a **step-by-step setup in Vietnamese**, see **[`README.md`](../README.md)** in the repo root.

---

## Open the right workspace

Cursor reads **`.cursor/**` from the folder you open** (File → Open Folder). That root must **directly** contain this `.cursor/` directory—not a parent folder that only has the project in a subfolder without `.cursor/`.

---

## Skills (`skills/`)

Each subfolder with a **`SKILL.md`** is a skill. Cursor discovers them under **`.cursor/skills/`**.

| How you use them | What to do |
|------------------|------------|
| **Slash / agent** | In chat, use a project command that points at a skill (e.g. **`/ck-cook`**) or ask the agent to follow the workflow in **`.cursor/skills/<name>/SKILL.md`**. |
| **Direct reference** | `@`-mention the file or path, e.g. **`.cursor/skills/ck-plan/SKILL.md`**, so the model loads that spec. |
| **Browse** | Open **`skills/`** and read `SKILL.md` + any `references/` next to it. |

Optional maintenance (normalize frontmatter / paths after bulk edits):

```bash
python3 .cursor/_fix_skills_frontmatter.py
python3 .cursor/_mirror_paths.py
```

(Run from the workspace root that contains `.cursor/`.)

---

## Rules (`rules/*.mdc`)

**`.mdc`** files are Cursor rules. **`globs`** in frontmatter limit which files each rule applies to (paths are relative to the **workspace root**).

Use them by leaving them in place; Cursor merges applicable rules into context when you work on matching files. Edit a rule file to change behavior project-wide.

---

## Slash commands (`commands/*.md`)

Only **top-level** **`commands/*.md`** reliably appear in the **`/`** command list. The command name is the **filename without `.md`**: e.g. **`ck-init.md`** → **`/ck-init`**.

Each file is a prompt template (YAML frontmatter + body). Use **`/…`** in Agent or chat to inject that template.

---

## Agents (`agents/*.md`)

Markdown agent definitions under **`agents/`** describe specialized agents (planner, researcher, etc.). Select or invoke them from Cursor’s agent UI according to your Cursor version; the source of truth for behavior is each **`.md`** file.

---

## Contexts (`contexts/*.md`)

Markdown “modes” (e.g. dev / research / review) used by optional hooks or by hand: open the matching file or let tooling inject it. Paths are always under **`.cursor/contexts/`**.

---

## Coding levels (`coding-levels/*.md`)

Style / level presets referenced by optional session tooling. Open the level you want or wire it through **`.ck.json`** if your workflow uses it.

---

## Hooks (`hooks/`)

Optional **Python** scripts (Claude Code–style events). They are **not** run by Cursor unless you add a compatible **`.cursor/hooks.json`**. Details: **[`hooks/README.md`](hooks/README.md)**.

---

## Layout reference

```text
.cursor/
  skills/           # one folder per skill → SKILL.md
  rules/*.mdc       # Cursor rules
  commands/*.md     # slash commands (flat filenames)
  agents/
  contexts/
  coding-levels/
  hooks/            # optional Python hooks
  _fix_skills_frontmatter.py
  _mirror_paths.py
```
