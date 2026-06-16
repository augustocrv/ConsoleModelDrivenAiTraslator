namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services;

/// <summary>
/// Encrypts and decrypts persisted API keys.
/// </summary>
/// <summary>Interface description.</summary>
public interface IApiKeyProtectorService
{
    /// <summary>
    /// Encrypts plain text.
    /// </summary>
    string Encrypt(string plainText);

    /// <summary>
    /// Decrypts cipher text.
    /// </summary>
    string Decrypt(string cipherText);
}



