using System.Text;

namespace EnigmaMod
{
    public static class MessagePreprocessor
    {
        public static string Preprocess(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            StringBuilder sb = new StringBuilder(text.Length);

            foreach (char c in text)
            {
                if (c == '.')
                {
                    sb.Append('X');
                }
                else if (c == ',')
                {
                    sb.Append("ZZ");
                }
                else if (c == '?')
                {
                    sb.Append("FRAGEZ");
                }
                else if (c >= '0' && c <= '9')
                {
                    switch (c)
                    {
                        case '0': sb.Append("NULL"); break;
                        case '1': sb.Append("EINS"); break;
                        case '2': sb.Append("ZWEI"); break;
                        case '3': sb.Append("DREI"); break;
                        case '4': sb.Append("VIER"); break;
                        case '5': sb.Append("FUENF"); break;
                        case '6': sb.Append("SECHS"); break;
                        case '7': sb.Append("SIEBEN"); break;
                        case '8': sb.Append("ACHT"); break;
                        case '9': sb.Append("NEUN"); break;
                    }
                }
                else if (c == 'ß')
                {
                    sb.Append("SS");
                }
                else if (char.IsLetter(c))
                {
                    sb.Append(char.ToUpperInvariant(c));
                }
            }

            return sb.ToString();
        }

        public static string FormatCiphertext(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            StringBuilder sb = new StringBuilder(text.Length + text.Length / 5);
            for (int i = 0; i < text.Length; i++)
            {
                if (i > 0 && i % 5 == 0)
                    sb.Append(' ');
                sb.Append(text[i]);
            }
            return sb.ToString();
        }
    }
}
