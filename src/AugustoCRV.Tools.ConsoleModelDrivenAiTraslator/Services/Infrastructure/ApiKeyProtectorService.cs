
namespace AugustoCRV.Tools.ConsoleModelDrivenAiTraslator.Services
{
    /// <summary>
    /// Provides simple AES-based encryption for storing API keys locally.
    /// </summary>
    /// <summary>Class description.</summary>
    public sealed class ApiKeyProtectorService : IApiKeyProtectorService
    {
        // Reuse the original static key/IV to preserve compatibility with existing stored connections.
        private static readonly byte[] Key = Convert.FromBase64String("bRFWl0n3WtiEpiHGCo/i+m1Fe3O3C1NAKlm/SgOoxM8=");
        private static readonly byte[] Iv = Convert.FromBase64String("tXo/irW24CHn/fGRMulMIQ==");

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
            {
                return plainText;
            }

            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = Iv;
            aes.Padding = PaddingMode.PKCS7;
            aes.Mode = CipherMode.CBC;

            using var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            using (var cryptoStream = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
            using (var writer = new StreamWriter(cryptoStream, Encoding.UTF8))
            {
                writer.Write(plainText);
            }

            return Convert.ToBase64String(ms.ToArray());
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
            {
                return cipherText;
            }

            using var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = Iv;
            aes.Padding = PaddingMode.PKCS7;
            aes.Mode = CipherMode.CBC;

            var buffer = Convert.FromBase64String(cipherText);
            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(buffer);
            using var cryptoStream = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var reader = new StreamReader(cryptoStream, Encoding.UTF8);
            {
                return reader.ReadToEnd();
            }
        }
    }
}



