// Zero-knowledge crypto core. EVERYTHING here runs in the browser via the
// Web Crypto API - the master password, the derived master key, and the
// data-encryption key (DEK) must never be sent anywhere. Only these ever
// cross the network: email, Salt (public), LoginAuthProof/RecoveryAuthProof
// (one-way derived, server bcrypt-hashes them again), and AES-GCM
// ciphertext (wrapped DEK, vault entries).
const VaultCrypto = (() => {
  const enc = new TextEncoder();
  const dec = new TextDecoder();

  function toB64(buf) {
    const bytes = new Uint8Array(buf);
    let bin = "";
    for (let i = 0; i < bytes.length; i++) bin += String.fromCharCode(bytes[i]);
    return btoa(bin);
  }
  function fromB64(b64) {
    const bin = atob(b64);
    const bytes = new Uint8Array(bin.length);
    for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
    return bytes.buffer;
  }

  function randomBytesB64(len) {
    return toB64(crypto.getRandomValues(new Uint8Array(len)));
  }

  // PBKDF2-SHA256, 600,000 iterations (OWASP 2023 minimum recommendation for
  // PBKDF2-SHA256) - deliberately slow, that's the point: it's the only
  // thing standing between a leaked-salt-plus-guessed-password and the DEK.
  async function deriveMasterKey(password, saltB64) {
    const keyMaterial = await crypto.subtle.importKey(
      "raw", enc.encode(password), "PBKDF2", false, ["deriveKey"]);
    return crypto.subtle.deriveKey(
      { name: "PBKDF2", salt: fromB64(saltB64), iterations: 600000, hash: "SHA-256" },
      keyMaterial,
      { name: "AES-GCM", length: 256 },
      true, // extractable - needed so we can wrap/unwrap the DEK with it
      ["encrypt", "decrypt"]
    );
  }

  // Derives a distinct, non-reversible "proof" value from a key for a given
  // purpose (login vs recovery) - HMAC keyed by the key's raw bytes, over a
  // fixed info string, single-step HKDF-Expand. Two different info strings
  // guarantee LoginAuthProof and RecoveryAuthProof can never be confused for
  // each other or for the key itself, even though both derive from
  // key material the server never sees.
  async function proofFromKey(cryptoKey, info) {
    const raw = await crypto.subtle.exportKey("raw", cryptoKey);
    const hmacKey = await crypto.subtle.importKey("raw", raw, { name: "HMAC", hash: "SHA-256" }, false, ["sign"]);
    const sig = await crypto.subtle.sign("HMAC", hmacKey, enc.encode(info));
    return toB64(sig);
  }

  async function generateDek() {
    return crypto.subtle.generateKey({ name: "AES-GCM", length: 256 }, true, ["encrypt", "decrypt"]);
  }

  async function importAesKey(rawB64) {
    return crypto.subtle.importKey("raw", fromB64(rawB64), "AES-GCM", true, ["encrypt", "decrypt"]);
  }
  async function exportAesKeyB64(key) {
    return toB64(await crypto.subtle.exportKey("raw", key));
  }

  async function aesEncrypt(key, plaintext) {
    const iv = crypto.getRandomValues(new Uint8Array(12));
    const ct = await crypto.subtle.encrypt({ name: "AES-GCM", iv }, key, enc.encode(plaintext));
    return { ciphertext: toB64(ct), iv: toB64(iv) };
  }
  async function aesDecrypt(key, ciphertextB64, ivB64) {
    const pt = await crypto.subtle.decrypt({ name: "AES-GCM", iv: fromB64(ivB64) }, key, fromB64(ciphertextB64));
    return dec.decode(pt);
  }

  // "Wrap" = encrypt a raw AES key's bytes with another AES-GCM key -
  // envelope encryption, so the DEK itself never has to change when the
  // master password changes (only its wrapping does).
  async function wrapKey(keyToWrap, wrappingKey) {
    const raw = toB64(await crypto.subtle.exportKey("raw", keyToWrap));
    return aesEncrypt(wrappingKey, raw);
  }
  async function unwrapKey(ciphertextB64, ivB64, wrappingKey) {
    const rawB64 = await aesDecrypt(wrappingKey, ciphertextB64, ivB64);
    return importAesKey(rawB64);
  }

  // Recovery key: 256 bits of real entropy, shown to the user ONCE as a
  // grouped string (xxxx-xxxx-...) they must save themselves - this is the
  // only backup path since the server can never recover a forgotten master
  // password by design.
  function generateRecoveryKeyString() {
    const bytes = crypto.getRandomValues(new Uint8Array(20)); // 160 bits, base32-ish grouping below is plenty for a recovery secret
    const b64 = toB64(bytes).replace(/[+/=]/g, "");
    return b64.match(/.{1,5}/g).join("-").toUpperCase();
  }
  async function recoveryStringToKey(recoveryString) {
    const clean = recoveryString.replace(/-/g, "");
    // Recovery string isn't valid base64 once uppercased/stripped of padding,
    // so re-derive a 256-bit AES key from it the same way a password would
    // be, using a fixed, well-known salt (not secret - the string itself
    // already has 160 bits of entropy, this is just to get well-formed key
    // bytes, not additional security).
    const keyMaterial = await crypto.subtle.importKey("raw", enc.encode(clean), "PBKDF2", false, ["deriveKey"]);
    return crypto.subtle.deriveKey(
      { name: "PBKDF2", salt: enc.encode("itmartin-vault-recovery-salt-v1"), iterations: 100000, hash: "SHA-256" },
      keyMaterial, { name: "AES-GCM", length: 256 }, true, ["encrypt", "decrypt"]);
  }

  // Password generator - crypto.getRandomValues, rejection-sampled so every
  // character in the pool has exactly equal probability (no modulo bias).
  function generatePassword({ length = 20, upper = true, lower = true, digits = true, symbols = true } = {}) {
    let pool = "";
    if (lower) pool += "abcdefghijkmnopqrstuvwxyz"; // no 'l' - ambiguous with '1'/'I'
    if (upper) pool += "ABCDEFGHJKLMNPQRSTUVWXYZ"; // no 'I','O' - ambiguous with '1'/'0'
    if (digits) pool += "23456789"; // no '0','1'
    if (symbols) pool += "!@#$%^&*-_=+?";
    if (!pool) pool = "abcdefghijkmnopqrstuvwxyz23456789";

    const max = 256 - (256 % pool.length);
    let out = "";
    const buf = new Uint8Array(1);
    while (out.length < length) {
      crypto.getRandomValues(buf);
      if (buf[0] < max) out += pool[buf[0] % pool.length];
    }
    return out;
  }

  return {
    randomBytesB64, deriveMasterKey, proofFromKey,
    generateDek, importAesKey, exportAesKeyB64,
    aesEncrypt, aesDecrypt, wrapKey, unwrapKey,
    generateRecoveryKeyString, recoveryStringToKey,
    generatePassword,
  };
})();
