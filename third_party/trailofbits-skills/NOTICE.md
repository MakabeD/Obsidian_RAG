# NOTICE — Vendored Trail of Bits skills

Copied subset of <https://github.com/trailofbits/skills> ("Trail of Bits Claude Code skills for security research, vulnerability detection, and audit workflows").

- **Source commit (pinned):** `123037ec8aed26f0d86327cc39137ee5043e5deb` (main branch at time of vendoring, 2026-09-17)
- **License:** Creative Commons Attribution-ShareAlike 4.0 International (CC BY-SA 4.0). See <https://creativecommons.org/licenses/by-sa/4.0/>. The upstream `LICENSE` text applies to every file in this directory.
- **Modifications:** files were relocated from `plugins/<name>/skills/<name>/` to `<name>/` and stripped of Claude-Code-specific agent definitions, assets, evals, and tests. No content was otherwise altered. The relocation constitutes "Adapted Material" under CC BY-SA 4.0; this directory therefore remains under CC BY-SA 4.0 and does not extend to the rest of this repository.

## Contents

| Directory | Upstream plugin | Used by |
| --- | --- | --- |
| `sharp-edges/` | `plugins/sharp-edges` | `audit-input-surface`, `audit-secrets-config` sub-skills |
| `insecure-defaults/` | `plugins/insecure-defaults` | `audit-secrets-config` sub-skill |
| `supply-chain-risk-auditor/` | `plugins/supply-chain-risk-auditor` | `audit-supply-chain` sub-skill |
| `fp-check/` | `plugins/fp-check` | `audit-fp-gate` sub-skill, orchestrator verification step |

## Updating

Re-pin deliberately: pick a new upstream commit, re-copy the same file set, update the SHA and date above, and review the diff as you would any third-party dependency. Do not point these files at a moving branch.
