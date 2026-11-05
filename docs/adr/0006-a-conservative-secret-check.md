# ADR-0006: A conservative secret check on publish

- **Status:** Accepted
- **Date:** 2026-11-05

## Context

Published notes reach every machine and the Git host. A secret pasted into a note by mistake should
not be published. Secret scanners that judge by names, for example reporting a variable *called*
`password` that only refers to an environment variable, produce false positives that teach users
to ignore or disable them.

## Decision

`memshare publish` checks the **added lines** of the changes it is about to commit and stops only on
values that look like real secrets:

- private key blocks (`-----BEGIN … PRIVATE KEY-----`);
- tokens with a recognisable, specific format (for example the documented prefixes and lengths of
  GitHub, GitLab, AWS access keys, Slack, Anthropic, OpenAI and Google API keys, JSON Web Tokens);
- credentials embedded in URLs (`scheme://user:secret@host`).

It never reports names, references to variables (`$VAR`, `${VAR}`, `%VAR%`, `$env:VAR`),
placeholders (`<token>`, `***`, `changeme`, `example`) or values that are too short. A finding
names the file, line and kind, never prints the value, and can be overridden for one publish
(`--allow-secrets`) or for one line with an inline marker.

## Consequences

- Some secrets with no recognisable format will pass. The rule "no secrets in shared memory" remains
  the user's, and the documentation says so.
- New formats are added as specific patterns with tests, including tests for what must not match.
