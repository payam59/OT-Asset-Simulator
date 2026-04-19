using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace OLRTLabSim.Helpers
{
    public static class CryptoHelper
    {
        private static readonly string MasterKey = "OLRTLabSim_Super_Secret_Master_Key_2024_!@#";

        private static (byte[] Key, byte[] IV) GetEncryptionKeys(string saltString)
        {
            using (var sha512 = SHA512.Create())
            {
                byte[] hash = sha512.ComputeHash(Encoding.UTF8.GetBytes(MasterKey + saltString));
                byte[] key = new byte[32];
                byte[] iv = new byte[16];
                Array.Copy(hash, 0, key, 0, 32);
                Array.Copy(hash, 32, iv, 0, 16);
                return (key, iv);
            }
        }

        public static string EncryptDeterministic(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;
            var keys = GetEncryptionKeys("deterministic_salt_for_lookups");
            return EncryptString(plainText, keys.Key, keys.IV);
        }

        public static string DecryptDeterministic(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;
            var keys = GetEncryptionKeys("deterministic_salt_for_lookups");
            return DecryptString(cipherText, keys.Key, keys.IV);
        }

        public static string EncryptRandom(string plainText)
        {
            if (string.IsNullOrEmpty(plainText)) return plainText;

            var keys = GetEncryptionKeys("base_key_for_random");

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = keys.Key;
                aesAlg.GenerateIV();

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    msEncrypt.Write(aesAlg.IV, 0, aesAlg.IV.Length);

                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }
                    }
                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }

        public static string DecryptRandom(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText)) return cipherText;

            var keys = GetEncryptionKeys("base_key_for_random");
            byte[] fullCipher = Convert.FromBase64String(cipherText);

            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = keys.Key;
                byte[] iv = new byte[16];
                Array.Copy(fullCipher, 0, iv, 0, iv.Length);
                aesAlg.IV = iv;

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(fullCipher, iv.Length, fullCipher.Length - iv.Length))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            return srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
        }

        private static string EncryptString(string plainText, byte[] key, byte[] iv)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key;
                aesAlg.IV = iv;

                ICryptoTransform encryptor = aesAlg.CreateEncryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msEncrypt = new MemoryStream())
                {
                    using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                    {
                        using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(plainText);
                        }
                    }
                    return Convert.ToBase64String(msEncrypt.ToArray());
                }
            }
        }

        private static string DecryptString(string cipherText, byte[] key, byte[] iv)
        {
            using (Aes aesAlg = Aes.Create())
            {
                aesAlg.Key = key;
                aesAlg.IV = iv;

                ICryptoTransform decryptor = aesAlg.CreateDecryptor(aesAlg.Key, aesAlg.IV);

                using (MemoryStream msDecrypt = new MemoryStream(Convert.FromBase64String(cipherText)))
                {
                    using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                    {
                        using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                        {
                            return srDecrypt.ReadToEnd();
                        }
                    }
                }
            }
        }
    }
}
