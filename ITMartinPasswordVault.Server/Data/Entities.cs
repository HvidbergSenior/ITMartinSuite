namespace ITMartinPasswordVault.Server.Data;

// Zero-knowledge design: the server NEVER sees the master password, the
// derived master key, or the data-encryption key (DEK) in plaintext.
//
//   - Salt: per-user PBKDF2 salt, client-side derives MasterKey from
//     (masterPassword, Salt). Public by design (salts aren't secret).
//   - AuthHash: server-side bcrypt hash of a client-derived "auth proof"
//     (itself derived from MasterKey via a distinct HKDF info string, so it
//     can never be turned back into MasterKey even if this hash leaks).
//   - WrappedDekByMaster / ...Iv: the real per-user AES-256 DEK (used to
//     encrypt every vault entry), AES-GCM-encrypted ("wrapped") under
//     MasterKey. Changing the master password only needs to re-wrap this,
//     never re-encrypt every entry.
//   - WrappedDekByRecovery / ...Iv: the same DEK, wrapped under a one-time
//     random recovery key shown to the user once at signup (their
//     responsibility to save it) - the only path back in if the master
//     password is forgotten, without the server ever being able to decrypt
//     anything itself.
public sealed class VaultUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = "";
    public string Salt { get; set; } = "";
    public string AuthHash { get; set; } = "";
    // bcrypt hash of a proof derived from the DEK itself (HKDF(DEK,
    // "recovery-auth")) - obtainable by unwrapping the DEK via EITHER the
    // master key or the recovery key, so this hash alone can't distinguish
    // which path was used. It exists purely so /api/recover/complete can
    // verify the caller actually possesses the real DEK (via the recovery
    // key) before letting them replace the master-password-derived fields -
    // without this, anyone who knew a user's email could reset their master
    // password with no proof of anything.
    public string RecoveryAuthHash { get; set; } = "";
    public string WrappedDekByMaster { get; set; } = "";
    public string WrappedDekByMasterIv { get; set; } = "";
    public string WrappedDekByRecovery { get; set; } = "";
    public string WrappedDekByRecoveryIv { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// One vault item (site/username/password/notes) - server only ever stores
// and returns the opaque AES-GCM ciphertext. Encryption/decryption happens
// entirely in the browser; this blob is meaningless without the DEK.
public sealed class VaultEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Ciphertext { get; set; } = "";
    public string Iv { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
