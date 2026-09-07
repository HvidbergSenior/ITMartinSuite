using ITMartin.Media.Contracts.Contracts.Runtime.Models;

namespace ITMartin.Media.Contracts.Contracts.Runtime.Interfaces;

// Delivers an already-finished local library to the NAS and wires it into
// the gallery-web app - the last two manual/ops steps in the runbook,
// automated 2026-09-07 ("thinking on harddisk and gallery. I also want that
// to be done."). Both shell out to real ssh/scp/tar/docker processes against
// the family's actual home NAS (10.0.0.126) - a materially different risk
// than every other service in this suite, which only ever touches the local
// filesystem. See NasDeliveryService's own comment for how that risk is
// contained (additive-only NAS push; idempotent, diff-checked gallery wiring
// via GalleryComposeEditor rather than a full YAML round-trip).
public interface INasDeliveryService
{
    // tar → scp -O → ssh-extract-into-scratch → cp -r → cleanup. Additive
    // only - writes to a new /volume1/docker/filesorter/library/<slug>/
    // folder, never touches an existing one unless the same slug is pushed
    // again (in which case it's a real re-delivery, same as running the old
    // manual steps twice would have done).
    Task<NasPushResult> PushToNasAsync(string libraryPath, string librarySlug, CancellationToken cancellationToken = default);

    // Fetches the live docker-compose.yaml, adds this gallery via
    // GalleryComposeEditor (no-op if already wired), pushes it back only if
    // it actually changed, then restarts gallery-web. password is a
    // required, caller-supplied argument - never generated or defaulted,
    // same "the one genuinely user-specific decision" the runbook has
    // called for since before this method existed.
    Task<GalleryWireResult> WireGalleryAsync(string librarySlug, string displayName, string password, CancellationToken cancellationToken = default);
}
