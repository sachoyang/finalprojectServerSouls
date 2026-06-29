# Codex Working Notes

- Keep token use low by default. Prefer narrow searches scoped to the relevant file or folder.
- Avoid broad `rg`, full-file dumps, and full `git diff` output unless the task truly needs them.
- At the start of a new conversation or a new feature area, first establish a reliable baseline by reading the complete relevant execution flow: entry point, called modules, data definitions, editor/runtime counterparts, and directly related prefabs or assets. Do not diagnose from an isolated snippet when behavior depends on multiple files.
- "Complete relevant execution flow" does not mean scanning the entire repository. Discover only the dependency path needed for the requested feature, then read those relevant files completely when necessary.
- After the baseline analysis, retain that understanding for the rest of the conversation. Use `git diff`, file timestamps, or narrowly scoped searches to identify changes, and reread only changed sections or code needed by the current task.
- Do not repeatedly dump or reread unchanged files. Reread a complete file only when it changed substantially, the remembered context is uncertain, or the current symptom contradicts the established model.
- Do not re-scan the overall project structure unless the user explicitly asks to read it again or the relevant dependency path cannot otherwise be established.
- Keep progress updates short and skip repeated explanations once context is established.
- When diagnosing visual/editor issues, verify the exact source before making broad guesses.
- After changes, report only the files changed and the essential verification result.
