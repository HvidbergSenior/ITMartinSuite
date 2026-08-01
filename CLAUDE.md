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

- FileSorter (`ITMartinFileSorter.Server`/`.Worker`) runs **locally only**, never against NAS/Z: paths for its own heavy per-file processing — network I/O per file puts real load on the Synology. Use local `C:\` paths.
- Never build Docker images on the NAS. Always use `.\deploy.ps1 -Service <name>` from the solution root — it builds locally and pushes the image.
- `docker-compose.yaml` on the NAS is the source of truth for per-tenant secrets (gallery passwords, admin PINs) — those live in `magic.env` (gitignored), never as plaintext values inline in `docker-compose.yaml`'s `environment:` block.
