namespace Beep.KocAiCommunity.Application.Admin;

/// <summary>
/// Encrypts and decrypts secret setting values. Implemented in the host with ASP.NET Data Protection
/// (dev/self-host) or a KMS-backed provider (production). Kept as an abstraction so the settings
/// service — and the rest of Infrastructure — stays free of ASP.NET dependencies.
/// </summary>
public interface ISecretProtector
{
    string Protect(string plaintext);
    string Unprotect(string ciphertext);
}
