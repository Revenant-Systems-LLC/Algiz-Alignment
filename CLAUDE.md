# Operating rules for Claude Code on this repo

Durable rules from the founder (Dave Fisher). These apply to every session,
not just the one they were set in.

## Credentials and secrets: ask first, always

Never inspect, list, `cat`, `grep`, `env`-dump, or otherwise touch `.env`
files, secrets directories (e.g. anything under a configured secrets drive),
credential stores, or environment variables that could contain API
keys/tokens/secrets, on this repo or any other repo belonging to Dave,
without asking him first and explaining exactly why.

This applies even when:
- The intent is a read-only existence check, not reading actual values.
- Output would be redacted before being shown.
- The check seems necessary to complete a task (e.g. "is a key available so
  I can run X").

If a task seems to require knowing whether a credential exists, ask the
user directly instead of checking yourself. No exceptions.
