using GrawiaaApp.API.Interfaces;

namespace GrawiaaApp.API.Interfaces
{
    public interface IEncryptionService
    {
        string Encrypt(string plainText);
        string Decrypt(string cipherText);
    }
}
