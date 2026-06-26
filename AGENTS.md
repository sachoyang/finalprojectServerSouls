# Codex Working Notes

- Keep token use low by default. Prefer narrow searches scoped to the relevant file or folder.
- Avoid broad `rg`, full-file dumps, and full `git diff` output unless the task truly needs them.
- Read only the smallest useful code region, then patch directly.
- Do not re-scan the overall project structure unless the user explicitly asks to read it again.
- Keep progress updates short and skip repeated explanations once context is established.
- When diagnosing visual/editor issues, verify the exact source before making broad guesses.
- After changes, report only the files changed and the essential verification result.
