using System.Text;

namespace EnigmaMod
{
    public static class MessagePreprocessor
    {
        public static string Preprocess(string text)
        {
            if (string.IsNullOrEmpty(text))
                return text;

            LanguageRules rules = PreprocessRules.ForLanguage(Localization.GetCurrentLanguage());

            string processed = text.ToUpperInvariant();
            if (rules.ReplaceChWithQ)
                processed = processed.Replace("CH", "Q");

            StringBuilder sb = new StringBuilder(processed.Length);
            StringBuilder digitRun = new StringBuilder();

            foreach (char c in processed)
            {
                if (c == 'ß')
                {
                    sb.Append("SS");
                    continue;
                }

                if (c >= '0' && c <= '9')
                {
                    digitRun.Append(c);
                    continue;
                }

                if (digitRun.Length > 0)
                {
                    sb.Append(ProcessDigitRun(digitRun.ToString(), rules));
                    digitRun.Clear();
                }

                if (rules.Punct.TryGetValue(c, out string replacement))
                {
                    sb.Append(replacement);
                }
                else if (char.IsLetter(c))
                {
                    sb.Append(c);
                }
            }

            if (digitRun.Length > 0)
                sb.Append(ProcessDigitRun(digitRun.ToString(), rules));

            return sb.ToString();
        }

        private static string ProcessDigitRun(string digits, LanguageRules rules)
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

                    while (zeroCount >= 4) { sb.Append(rules.Zero4); zeroCount -= 4; }
                    while (zeroCount >= 3) { sb.Append(rules.Zero3); zeroCount -= 3; }
                    while (zeroCount >= 2) { sb.Append(rules.Zero2); zeroCount -= 2; }
                    while (zeroCount >= 1) { sb.Append(rules.Digits['0']); zeroCount--; }
                }
                else
                {
                    sb.Append(rules.Digits[digits[i]]);
                    i++;
                }
            }
            return sb.ToString();
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
