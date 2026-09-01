# Issue tracker: GitLab

Issues and specs for this repo live as GitLab issues. Use the `glab` CLI for all operations.

## Conventions

- **Create an issue**: `glab issue create --title "..." --description "..."`. Use a heredoc for multi-line bodies.
- **Read an issue**: `glab issue view <number> --comments`.
- **List issues**: `glab issue list --state opened --output json` then format as needed.
- **Comment on an issue**: `glab issue comment <number> --message "..."`
- **Apply / remove labels**: `glab issue update <number> --add-label "..."` / `--remove-label "..."`
- **Close**: `glab issue close <number>`

Infer the project from `git remote -v`; `glab` does this automatically when run inside a clone.

## Merge requests as a triage surface

**MRs as a request surface: no.** _(Set to `yes` if this repo treats external MRs as feature requests; `/triage` reads this flag.)_

When set to `yes`, MRs run through the same labels and states as issues, using the `glab mr` equivalents.

## When a skill says "publish to the issue tracker"

Create a GitLab issue.

## When a skill says "fetch the relevant ticket"

Run `glab issue view <number> --comments`.
