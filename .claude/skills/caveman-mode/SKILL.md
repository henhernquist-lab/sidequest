# Caveman Mode

## What this skill is

A communication style override for low-stakes confirmations. When triggered, responses are short and blunt in caveman-style phrasing to save output tokens.

## Trigger condition

Triggers on **simple task confirmations only** — things like:
- "I made the file"
- "task done"
- "here's the folder structure"
- File created / edited / deleted confirmations
- Simple structural reports (folder listings, file counts)

Trigger is **automatic** when the user's request is a straightforward status ping with no ambiguity.

## Output style

Short, blunt, caveman-style:
- "File made. Code good. No bug."
- "Done. Two files changed."
- "Folder there. Six items."

One or two sentences max. No filler, no hedging, no "I've successfully..." preludes.

## When this skill is OFF (use full normal English, no exceptions)

This skill must NOT activate for any of these categories. If the request falls into any of them, ignore this skill entirely and respond in clear normal English:

1. **Bug reports or anything explaining WHY something failed** — causation needs precision, caveman-speak loses too much.
2. **Verification results** — did a test actually pass, did a build actually succeed, is something verified vs unverified. The project's verification discipline (see AGENTS.md) depends on this distinction surviving intact. "Unverified" vs "verified" gets garbled by caveman mode and that's dangerous.
3. **Anything touching the data model, LOD architecture, or utility-AI scoring** — the "Henry reviews" list from AGENTS.md. These are design-sensitive and need full clarity.
4. **Any STATUS.md entry** — those are the permanent record, readable later by sessions with no memory of this joke. They need to be actually clear.
5. **Anything requiring real precision** — if there's any doubt, default to normal English.

## Decision rule

If genuinely unsure whether something is "simple enough" for caveman mode, default to normal English. A confirmation too garbled to be sure of costs more time re-asking than the tokens it saved.

## How to apply

When the user types `/caveman` or otherwise invokes this skill:
1. Read the request.
2. Check the trigger condition and the OFF categories above.
3. If it's a simple confirmation and not in an OFF category — respond in caveman style.
4. If it's ambiguous or in an OFF category — respond in normal English and do not use caveman phrasing.
