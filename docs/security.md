# Security

## Trust model

What is published to a store becomes **instructions** in every session on every machine that uses
the store. Treat a store like code that runs everywhere:

- Only you write to it: the agent's edits of a store need your approval, and nothing is sent until
  you publish.
- Review `memshare diff` before publishing.
- Keep stores **private** on your Git host, and give write access only to your own machines' deploy
  keys.
- Store only what you would be comfortable having in every session. Never store secrets.

## Keys

- Each machine has its **own deploy key per store** (`~/.ssh/memshare-<store>_ed25519`), with access to
  that one repository only. Revoke a lost or retired machine by deleting its key on the Git host. The
  key has no passphrase, so the session-start hook can run unattended. It is protected by your user
  account's file permissions.
- The Git host's SSH **host key is pinned** at `store add`, after you confirm its fingerprint, in
  memshare's own `known_hosts`. Git then refuses to connect if the host's key changes.
- memshare's SSH command uses only that key and that `known_hosts`: not your SSH agent, not your
  personal SSH configuration.
- Commits use one neutral author (`memshare`), so the history doesn't say which machine made a change.

## Secrets

`publish` checks the lines it is about to send for values with the format of real secrets: private
keys, tokens of GitHub, GitLab, AWS, Slack, Anthropic, OpenAI, Google, Stripe, npm and Hugging Face,
JSON Web Tokens, and passwords in URLs. The check is deliberately narrow, to avoid false alarms, so
secrets without a recognisable format can pass. It is a safety net, not a permission.

## The executable

- Releases are built by the repository's public workflow and published with `SHA256SUMS` and signed
  build provenance. The install scripts verify the archive against `SHA256SUMS` before running
  anything; to also check that an archive was built by this repository's workflow:
  `gh attestation verify memshare-linux-x64.tar.gz --repo praaline/agent-memshare`.
- memshare never updates itself: a new version is installed only when you run the install script or
  `memshare install` from a newer executable.
- It runs `git` and, for `store add`, `ssh-keygen` and `ssh-keyscan`, with arguments passed directly,
  never through a shell.

## Reporting a vulnerability

Please report security issues privately through the repository's security advisories ("Report a
vulnerability"), not in public issues.
