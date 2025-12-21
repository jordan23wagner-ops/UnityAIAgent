#!/usr/bin/env bash
set -euo pipefail

OUT="Docs/EquipmentDamage_MVP_CodeDump.md"

changed=$(git diff --name-only -- '*.cs' || true)
untracked=$(git ls-files --others --exclude-standard -- '*.cs' || true)

# Build unique sorted list
files=$(printf "%s\n%s\n" "$changed" "$untracked" | sed '/^$/d' | sort -u)

{
  echo "# Equipment Damage MVP - Code Dump"
  echo
  echo "Generated: $(date '+%Y-%m-%d %H:%M:%S')"
  echo
  echo "## Files"
  while IFS= read -r f; do
    echo "- $f"
  done <<< "$files"

  while IFS= read -r f; do
    echo
    echo "---"
    echo
    echo "## $f"
    echo
    echo "\`\`\`csharp"
    if [[ -f "$f" ]]; then
      # Strip CR for clean markdown diff/viewing
      sed 's/\r$//' "$f"
      [[ $(tail -c 1 "$f" | wc -l) -eq 0 ]] || true
    else
      echo "// ERROR: file not found"
    fi
    echo "\`\`\`"
  done <<< "$files"
} > "$OUT"

echo "Wrote $OUT"