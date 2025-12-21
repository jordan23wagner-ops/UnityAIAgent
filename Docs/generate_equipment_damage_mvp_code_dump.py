from __future__ import annotations

import subprocess
from pathlib import Path
from datetime import datetime


def _run_git(args: list[str]) -> list[str]:
    completed = subprocess.run(
        ["git", *args],
        check=True,
        capture_output=True,
        text=True,
    )
    return [line.strip() for line in completed.stdout.splitlines() if line.strip()]


def main() -> None:
    repo_root = Path(__file__).resolve().parents[1]
    output_path = repo_root / "Docs" / "EquipmentDamage_MVP_CodeDump.md"

    changed = _run_git(["diff", "--name-only", "--", "*.cs"])
    untracked = _run_git(["ls-files", "--others", "--exclude-standard", "--", "*.cs"])

    files = sorted(set(changed + untracked))

    lines: list[str] = []
    lines.append("# Equipment Damage MVP - Code Dump")
    lines.append("")
    lines.append(f"Generated: {datetime.now().strftime('%Y-%m-%d %H:%M:%S')}")
    lines.append("")
    lines.append("## Files")
    lines.extend([f"- {f}" for f in files])

    for rel in files:
        p = repo_root / rel
        lines.append("")
        lines.append("---")
        lines.append("")
        lines.append(f"## {rel}")
        lines.append("")
        lines.append("```csharp")
        if not p.exists():
            lines.append("// ERROR: file not found")
        else:
            # Preserve file contents exactly; normalize newlines to \n in output.
            content = p.read_text(encoding="utf-8", errors="replace").replace("\r\n", "\n")
            lines.append(content.rstrip("\n"))
        lines.append("```")

    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(f"Wrote {output_path}")


if __name__ == "__main__":
    main()
