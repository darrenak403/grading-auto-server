"""
Count hooks, skills, and agents under a kit directory (project `.cursor/`).

Usage:
    from config_counter import get_summary

    summary = get_summary(Path(".cursor"))
    # {"hooks": 0, "skills": 5, "agents": 3}
"""
import json
from pathlib import Path


def count_hooks(settings_path: Path) -> int:
    """Count total hook entries registered in settings.json (Claude Code style)."""
    if not settings_path.exists():
        return 0
    try:
        cfg = json.loads(settings_path.read_text(encoding="utf-8-sig"))
        hooks_map = cfg.get("hooks", {})
        total = 0
        for event_entries in hooks_map.values():
            for matcher_block in event_entries:
                total += len(matcher_block.get("hooks", []))
        return total
    except Exception:
        return 0


def count_skills(kit_dir: Path) -> int:
    """Count skill directories under <kit>/skills/."""
    skills_dir = kit_dir / "skills"
    if not skills_dir.exists():
        return 0
    return sum(1 for p in skills_dir.iterdir() if p.is_dir() and not p.name.startswith("."))


def count_agents(kit_dir: Path) -> int:
    """Count agent .md files under <kit>/agents/."""
    agents_dir = kit_dir / "agents"
    if not agents_dir.exists():
        return 0
    return sum(1 for p in agents_dir.iterdir() if p.is_file() and p.suffix == ".md")


def get_summary(kit_dir: Path) -> dict:
    """Return {hooks, skills, agents} counts for a kit directory (e.g. .cursor/)."""
    return {
        "hooks": count_hooks(kit_dir / "settings.json"),
        "skills": count_skills(kit_dir),
        "agents": count_agents(kit_dir),
    }
