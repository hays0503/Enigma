using System.Text;
using UnityEngine;

namespace EnigmaMod
{
    public static class MessagePreprocessor
    {
        public static string Preprocess(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                Debug.Log("[EnigmaMod] Preprocess: input is null or empty");
                return text;
            }

            string lang = Localization.GetCurrentLanguage();
            LanguageRules rules = PreprocessRules.ForLanguage(lang);

            Debug.Log($"[EnigmaMod] Preprocess: lang={lang}, inputLen={text.Length}, inputPreview='{Truncate(text, 60)}'");

            string processed = text.ToUpperInvariant();
            if (rules.ReplaceChWithQ)
            {
                processed = processed.Replace("CH", "Q");
                Debug.Log("[EnigmaMod] Preprocess: replaced CH→Q (German rule)");
            }

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

            string result = sb.ToString();
            Debug.Log($"[EnigmaMod] Preprocess: outputLen={result.Length}, outputPreview='{Truncate(result, 60)}'");
            return result;
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
            string result = sb.ToString();
            Debug.Log($"[EnigmaMod] Preprocess.ProcessDigitRun: digits='{digits}' → '{result}' (len={result.Length})");
            return result;
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
            string result = sb.ToString();
            Debug.Log($"[EnigmaMod] FormatCiphertext: inputLen={text.Length}, outputLen={result.Length}, preview='{Truncate(result, 60)}'");
            return result;
        }

        private static string Truncate(string s, int max)
        {
            return s.Length <= max ? s : s.Substring(0, max) + "...";
        }
    }
}
