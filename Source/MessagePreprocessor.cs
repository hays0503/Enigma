using System.Text;

namespace EnigmaMod
{
    public static class MessagePreprocessor
    {
        public static string Preprocess(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            text = text.ToUpperInvariant().Replace("CH", "Q");

            StringBuilder sb = new StringBuilder(text.Length);
            StringBuilder digitRun = new StringBuilder();

            foreach (char c in text)
            {
                if (c >= '0' && c <= '9')
                {
                    digitRun.Append(c);
                    continue;
                }

                if (digitRun.Length > 0)
                {
                    sb.Append(ProcessDigitRun(digitRun.ToString()));
                    digitRun.Clear();
                }

                switch (c)
                {
                    case '.': sb.Append('X'); break;
                    case ',': sb.Append('Y'); break;
                    case '?': sb.Append("FRAGE"); break;
                    case ':': sb.Append("XX"); break;
                    case '-': case '–': case '—': case '/': sb.Append("YY"); break;
                    case '(': case ')': sb.Append("KK"); break;
                    case 'ß': sb.Append("SS"); break;
                    default:
                        if (char.IsLetter(c))
                            sb.Append(c);
                        break;
                }
            }

            if (digitRun.Length > 0)
                sb.Append(ProcessDigitRun(digitRun.ToString()));

            return sb.ToString();
        }

        private static string ProcessDigitRun(string digits)
        {
            StringBuilder sb = new StringBuilder();
            int i = 0;
            while (i < digits.Length)
            {
                if (digits[i] == '0')
                {
                    int start = i;
                    while (i < digits.Length && digits[i] == '0')
                        i++;
                    int zeroCount = i - start;

                    while (zeroCount >= 4) { sb.Append("MYRIA"); zeroCount -= 4; }
                    while (zeroCount >= 3) { sb.Append("MILLE"); zeroCount -= 3; }
                    while (zeroCount >= 2) { sb.Append("CENTA"); zeroCount -= 2; }
                    while (zeroCount >= 1) { sb.Append("NULL"); zeroCount--; }
                }
                else
                {
                    sb.Append(DigitToWord(digits[i]));
                    i++;
                }
            }
            return sb.ToString();
        }

        private static string DigitToWord(char d)
        {
            switch (d)
            {
                case '0': return "NULL";
                case '1': return "EINZ";
                case '2': return "ZWO";
                case '3': return "DREI";
                case '4': return "VIER";
                case '5': return "FUNF";
                case '6': return "SEQS";
                case '7': return "SIEBEN";
                case '8': return "AQT";
                case '9': return "NEUN";
                default: return "";
            }
        }

        public static string FormatCiphertext(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            StringBuilder sb = new StringBuilder(text.Length + text.Length / 4);
            for (int i = 0; i < text.Length; i++)
            {
                if (i > 0 && i % 4 == 0)
                    sb.Append(' ');
                sb.Append(text[i]);
            }
            return sb.ToString();
        }
    }
}
