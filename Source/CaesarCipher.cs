using System.Text;

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
                return text;

            var sb = new StringBuilder(text.Length);
            foreach (char c in text)
            {
                sb.Append(ShiftChar(c));
            }
            return sb.ToString();
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
    }
}
