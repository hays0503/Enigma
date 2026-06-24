using System.Text;
using UnityEngine;

namespace EnigmaMod
{
    public static class CaesarCipher
    {
        private const int Shift = 13;

        private const string LatinLower = "abcdefghijklmnopqrstuvwxyz";
        private const string LatinUpper = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string CyrillicLower = "абвгдеёжзийклмнопрстуфхцчшщъыьэюя";
        private const string CyrillicUpper = "АБВГДЕЁЖЗИЙКЛМНОПРСТУФХЦЧШЩЪЫЬЭЮЯ";

        public static string Encrypt(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                Debug.Log("[EnigmaMod] CaesarCipher.Encrypt: input is null or empty");
                return text;
            }

            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                sb.Append(ShiftChar(c));
            }
            string result = sb.ToString();
            Debug.Log($"[EnigmaMod] CaesarCipher.Encrypt: inputLen={text.Length}, outputLen={result.Length}, preview='{Truncate(result, 50)}'");
            return result;
        }

        public static string Decrypt(string text)
        {
            return Encrypt(text);
        }

        private static char ShiftChar(char c)
        {
            int idx;

            idx = LatinUpper.IndexOf(c);
            if (idx != -1)
                return LatinUpper[(idx + Shift) % 26];

            idx = LatinLower.IndexOf(c);
            if (idx != -1)
                return LatinLower[(idx + Shift) % 26];

            idx = CyrillicUpper.IndexOf(c);
            if (idx != -1)
                return CyrillicUpper[(idx + Shift) % 33];

            idx = CyrillicLower.IndexOf(c);
            if (idx != -1)
                return CyrillicLower[(idx + Shift) % 33];

            return c;
        }

        private static string Truncate(string s, int max)
        {
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }
    }
}
