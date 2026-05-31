# llm-docs

Reference material assembled for LLM assistants working on this mod. Read on demand — these are not all relevant to every task.

## Index

- **`game-model.md`** — Wasteland 2 Director's Cut structural reference: spatial model, party system, combat mechanics, inventory, dialogue, UI screen list, default controls, save format, modding context. Read when you need a mental model of how the game works in order to make accessibility decisions you can't infer from the code alone.

## How this is meant to be used

The repo's root `CLAUDE.md` is loaded into context automatically. Files under `llm-docs/` are not — open them when the task at hand calls for them. Game-mechanics or wording questions usually warrant `game-model.md`; pure code-architecture questions usually do not.

If you discover a recurring topic that future sessions will need, add a new file here and link it from this index. Keep the index entries short — one line per file describing when to open it, not what it contains in detail.
