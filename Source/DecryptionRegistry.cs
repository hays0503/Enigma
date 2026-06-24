using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace EnigmaMod
{
    [Serializable]
    public class DecryptionState
    {
        public string MessageId;
        public string StartTime;
        public int TotalChars;
        public bool IsDecrypted;
        public string Ciphertext;
        public string Plaintext;
    }

    [Serializable]
    public class DecryptionSaveData
    {
        public List<DecryptionState> States;
    }

    public static class DecryptionRegistry
    {
        private static readonly Dictionary<string, DecryptionState> states = new Dictionary<string, DecryptionState>();
        private static string savePath;
        private static bool initialized;

        public static Action<string> OnDecryptionCompleted;

        public static void Init()
        {
            if (initialized)
                return;
            initialized = true;
            savePath = Path.Combine(Application.persistentDataPath, "EnigmaMod", "decryption.json");
            Load();
        }

        public static bool IsDecrypted(string messageId)
        {
            Init();
            if (states.TryGetValue(messageId, out var s))
            {
                if (s.IsDecrypted)
                    return true;

                int expired = GetProgress(messageId);
                return s.IsDecrypted;
            }
            return false;
        }

        public static int GetProgress(string messageId)
        {
            Init();
            if (!states.TryGetValue(messageId, out var state))
                return -1;
            if (state.IsDecrypted)
                return state.TotalChars;

            DateTime startTime;
            if (!DateTime.TryParse(state.StartTime, out startTime))
                return 0;

            var elapsed = DateTime.Now - startTime;
            int revealed = Math.Min(state.TotalChars, (int)(elapsed.TotalSeconds / 4.0));

            if (revealed >= state.TotalChars)
            {
                state.IsDecrypted = true;
                Save();
                if (OnDecryptionCompleted != null)
                {
                    try { OnDecryptionCompleted(messageId); }
                    catch { }
                }
            }

            return revealed;
        }

        public static void StartDecryption(string messageId, int totalChars, string ciphertext, string plaintext)
        {
            Init();
            if (states.ContainsKey(messageId) && states[messageId].IsDecrypted)
                return;

            if (states.ContainsKey(messageId) && !states[messageId].IsDecrypted)
                return;

            states[messageId] = new DecryptionState
            {
                MessageId = messageId,
                StartTime = DateTime.Now.ToString("O"),
                TotalChars = totalChars,
                IsDecrypted = false,
                Ciphertext = ciphertext,
                Plaintext = plaintext
            };
            Save();
        }

        public static string GetCiphertext(string messageId)
        {
            Init();
            DecryptionState state;
            if (states.TryGetValue(messageId, out state))
                return state.Ciphertext;
            return null;
        }

        public static string GetPlaintext(string messageId)
        {
            Init();
            DecryptionState state;
            if (states.TryGetValue(messageId, out state))
                return state.Plaintext;
            return null;
        }

        private static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var data = new DecryptionSaveData { States = new List<DecryptionState>(states.Values) };
                File.WriteAllText(savePath, JsonUtility.ToJson(data, true));
            }
            catch (Exception e)
            {
                Debug.LogError("[EnigmaMod] Failed to save decryption registry: " + e.Message);
            }
        }

        private static void Load()
        {
            try
            {
                if (!File.Exists(savePath))
                    return;

                var data = JsonUtility.FromJson<DecryptionSaveData>(File.ReadAllText(savePath));
                if (data?.States != null)
                {
                    states.Clear();
                    foreach (var state in data.States)
                        states[state.MessageId] = state;
                }
            }
            catch (Exception e)
            {
                Debug.LogError("[EnigmaMod] Failed to load decryption registry: " + e.Message);
            }
        }
    }
}
