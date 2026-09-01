# Issue tracker: local markdown

Issues and specs for this repo live as plain Markdown files under `.scratch/`. There is no remote tracker; everything stays in the working tree.

## Conventions

- **Layout**: one folder per issue, `.scratch/<kebab-case-slug>/`, containing an `issue.md` (the spec) and any supporting files.
- **Numbering**: a 4-digit zero-padded prefix per issue, assigned in chronological order. Example: `.scratch/0001-session-persistence/issue.md`.
- **Status**: a leading badge in the body, e.g. `> **Status**: open | ready | in-progress | closed`.
- **Create an issue**: make the folder, write `issue.md`, commit.
- **Read an issue**: open `.scratch/<n>-<slug>/issue.md`.
- **List open issues**: `ls -d .scratch/*/ | sort` then read each `issue.md` and filter by the status line.
- **Comment on an issue**: append a `## Comment` section to the file with author + ISO date.
- **Close**: flip the status badge to `closed` and add a closing comment.

## When a skill says "publish to the issue tracker"

Create a new `.scratch/<next-number>-<slug>/issue.md` with a stub body, then continue.

## When a skill says "fetch the relevant ticket"

Open `.scratch/<n>-<slug>/issue.md` and read it.

## When a skill says "append to a ticket"

Edit the existing `issue.md` and add a dated `## Note` or `## Decision` section.
