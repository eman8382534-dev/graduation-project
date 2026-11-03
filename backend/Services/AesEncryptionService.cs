using System.Security.Cryptography;
using System.Text;
using GrawiaaApp.API.Interfaces;
using Microsoft.Extensions.Configuration;

namespace GrawiaaApp.API.Services
{
    public class AesEncryptionService : IEncryptionService
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;

        public AesEncryptionService(IConfiguration config)
        {
            var rawKey = Encoding.UTF8.GetBytes(config["Encryption:Key"] ?? "GrowiaSecretKey2026_32Chars_Fixd");
            var rawIv  = Encoding.UTF8.GetBytes(config["Encryption:IV"]  ?? "GrowiaIV16CharsX");

            // AES-256 requires exactly 32 bytes; AES-128 requires 16. Resize defensively.
            _key = new byte[32];
            _iv  = new byte[16];
            Buffer.BlockCopy(rawKey, 0, _key, 0, Math.Min(rawKey.Length, 32));
            Buffer.BlockCopy(rawIv,  0, _iv,  0, Math.Min(rawIv.Length,  16));
        }

        public string Encrypt(string plainText)
        {
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;

            var encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream();
            using var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write);
            using var sw = new StreamWriter(cs);
            sw.Write(plainText);
            sw.Close();
            return Convert.ToBase64String(ms.ToArray());
        }

        public string Decrypt(string cipherText)
        {
            var buffer = Convert.FromBase64String(cipherText);
            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;

            var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var ms = new MemoryStream(buffer);
            using var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read);
            using var sr = new StreamReader(cs);
            return sr.ReadToEnd();
        }
    }
}
