# ITMartinSuite — instructions for Claude

## NEVER delete customer source data (highest priority rule in this file)

**Never run `rm` (or any delete) against a FileSorter *source* path.** On the photoserver that is
anything under `/home/martinhvidberg/filesorter-personal/jobs/` (`/jobs/...` inside the
containers). The same applies to any customer drive or folder a job was copied from.

**Why this rule exists:** a customer's photos frequently exist in exactly ONE place — the copy on
the photoserver — because the drive they came from gets reused as the *output* target for the
sorted library. The sorted output is NOT a backup: it is deliberately smaller (screenshots, memes,
chat images and duplicates are excluded, and duplicates collapse to one copy). On the ToshibaTest
job the source was 87,009 files while the delivered output was 34,079 photos. Deleting the source
because "it's already sorted" therefore destroys ~53,000 files that exist nowhere else.

**How to apply:**
- The pipeline itself only ever reads its source. Keep it that way; no step may write to or delete
  from the source path.
- If something under `jobs/` genuinely must go (a half-finished transfer, a wrong upload), **move
  it aside with `mv` instead of deleting**, list what you are about to touch first, and tell the
  user what you are doing and why before you do it.
- Never delete a source copy to free disk space. Free space from Docker (`docker image prune`),
  build artifacts, or old *output* instead — and if that is not enough, ask.
- Before any destructive command on the photoserver, state the full path out loud and confirm it
  is an output/generated path, not a source path.

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
  - **2026-09-03: default run location is now the photoserver** (10.0.0.200, separate physical machine from the Synology NAS — 8 cores, 12GB RAM, Docker), not this desktop. This is a "local disk on that machine" deployment, same as the rule above — it does NOT mean running against the NAS's network share, which is still forbidden. Slower per-run than a many-core desktop (fewer cores), but always-on and doesn't tie up a personal machine.
    - **`deploy.ps1` does not support the photoserver yet** — it's hardcoded to the NAS (`$NasLocal`/`$NasTailscale`), and its `docker-compose.yaml` lives on the NAS itself, not in this repo. Getting FileSorter's containers running on the photoserver needs either extending `deploy.ps1` with a photoserver target, or checking what compose setup the photoserver's own ~27 `martinsuite-*` containers already use (see [[reference_photoserver]]) and adding filesorter-web/filesorter-worker to that. Neither has been done — don't assume the app is deployable there yet.
    - Per-run **media data** (source folder to sort) is the "just copy a folder and run it" part — scp/copy it to local disk on the photoserver once the containers exist there, same local-disk-only rule as above.
- Never build Docker images on the NAS. Always use `.\deploy.ps1 -Service <name>` from the solution root — it builds locally and pushes the image.
- `docker-compose.yaml` on the NAS is the source of truth for per-tenant secrets (gallery passwords, admin PINs) — those live in `magic.env` (gitignored), never as plaintext values inline in `docker-compose.yaml`'s `environment:` block.
