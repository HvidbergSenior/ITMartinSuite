# ITMartinSuite — instructions for Claude

## AI/Claude API cost discipline (critical, stated repeatedly by the user)

**NEVER write code that makes one Claude API call per file in a loop.** This applies to every AI-driven feature in this suite (image tagging, captions, face/object detection, OCR cleanup, anything that calls Claude per photo/document). A real customer library can be tens of thousands of files — a 1:1 call-per-file pattern is the single most expensive mistake to make here, and it has happened more than once.

Instead:
- **Batch multiple files into one API call.** Claude vision accepts several images in a single message; use a tool schema that returns an array of per-item results (index + result) rather than one call per item.
- **Enforce a hard cap in code**, not just a comment — every per-file AI pass must have an actual `MaxCallsPerRun`-style ceiling that stops the run (or requires an explicit override) rather than a note that's easy to skip past under deadline pressure. A cap you can forget to check is not a cap.
- **Prefer cheap models** (Haiku, not Opus/Sonnet) for bulk per-photo work.
- **Make re-runs incremental**, not full-sweeps — skip already-processed items so a second run only costs what's new, not the whole library again (but incremental-skip alone does not protect the *first* run against an unexpectedly large file count — the hard cap above is what does that).
- **Concurrency is not a cost fix.** Running calls in parallel makes a bad per-file ratio burn through budget *faster*, not cheaper. Fix the ratio first (batching), then consider concurrency for whatever's left.

If you're about to write a `foreach`/`Parallel.ForEachAsync` loop that calls a Claude service once per file, stop and batch it instead.

## Deploy / environment rules

- FileSorter (`ITMartinFileSorter.Server`/`.Worker`) must always process files on **local disk relative to wherever it's actually running** — never over a network path (SMB share, NAS mount) for its own heavy per-file work. Source/library folders must sit on that machine's own storage.
  - **Why this rule exists:** the deployment of `filesorter-worker` running directly on the Synology NAS crashed with an unhandled `RabbitMQ.Client.Exceptions.BrokerUnreachableException` (couldn't reach the `rabbitmq` container — connection timed out, no retry/reconnect handling) and `filesorter-web` was separately OOM-killed (exit 137). Both sat dead for 7 weeks without anyone noticing, during which `jobs/mie/` accumulated 13+ duplicate `iCloud-fotos (1)`–`(13)` folders from repeated failed import attempts that kept retrying against a worker that was never coming back. Local disk avoids both the resource contention that triggered this and the silent-failure blast radius of a shared always-on NAS service.
  - **2026-09-03: default run location is now the photoserver** (10.0.0.200, separate physical machine from the Synology NAS — 8 cores, 12GB RAM, Docker), not this desktop. This is a "local disk on that machine" deployment, same as the rule above — it does NOT mean running against the NAS's network share, which is still forbidden. Slower per-run than a many-core desktop (fewer cores), but always-on and doesn't tie up a personal machine. Deploy via `.\deploy.ps1 -Service filesorter` per the rule below, not by hand-copying files.
- Never build Docker images on the NAS. Always use `.\deploy.ps1 -Service <name>` from the solution root — it builds locally and pushes the image.
- `docker-compose.yaml` on the NAS is the source of truth for per-tenant secrets (gallery passwords, admin PINs) — those live in `magic.env` (gitignored), never as plaintext values inline in `docker-compose.yaml`'s `environment:` block.
