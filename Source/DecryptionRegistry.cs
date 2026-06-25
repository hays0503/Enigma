using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DWS.Common.InjectionFramework;
using UBOAT.Game.Core.Time;
using UBOAT.Game.Sandbox;
using UnityEngine;

namespace EnigmaMod
{
    [Serializable]
    public class DecryptionState
    {
        public string MessageId;
        public long StartTick;
        public int TotalChars;
        public int PlaintextLength;
        public bool IsDecrypted;
        public string Ciphertext;
        public string Plaintext;
        public int RevealedCount;
    }

    [Serializable]
    public class DecryptionSaveData
    {
        public string CampaignId;
        public List<DecryptionState> States;
    }

    public static class DecryptionRegistry
    {
        private static readonly Dictionary<string, DecryptionState> states = new Dictionary<string, DecryptionState>();
        private static string savePath;
        private static bool initialized;
        private static GameTime gameTime;
        private static long lastSpeedRealTick;
        private static long lastSpeedGameTick;
        private static double cachedGameSpeed = 1.0;
        private const long TicksPerChar = 10000000L;
        private const long SaveIntervalTicks = 5 * TimeSpan.TicksPerSecond;
        private const string LogTag = "[EnigmaMod] DecryptionRegistry";
        private static long lastSaveRealTick;

        private static bool TryGetGameTime()
        {
            if (gameTime != null)
                return true;

            gameTime = InjectionFramework.Instance.GetInstance<GameTime>();
            if (gameTime != null)
                Debug.Log($"{LogTag}.Init: GameTime obtained, currentDateTime={gameTime.CurrentDateTime:O}");
            else
                Debug.LogError($"{LogTag}.TryGetGameTime: FAILED to get GameTime instance");

            return gameTime != null;
        }

        private static double GetGameSpeed()
        {
            if (gameTime != null)
            {
                var type = gameTime.GetType();
                var prop = type.GetProperty("TimeScale") ?? type.GetProperty("Speed") ?? type.GetProperty("TimeMultiplier");
                if (prop != null)
                {
                    var val = prop.GetValue(gameTime, null);
                    if (val != null)
                    {
                        try
                        {
                            double s = Convert.ToDouble(val);
                            if (s > 0)
                            {
                                cachedGameSpeed = s;
                                return s;
                            }
                        }
                        catch
                        {
                            // свойство существует, но это не число (например, Parameter) — fallback на дельта-трекинг
                        }
                    }
                }
            }

            long nowReal = DateTime.UtcNow.Ticks;
            long nowGame = gameTime?.CurrentDateTime.Ticks ?? nowReal;

            if (lastSpeedRealTick != 0 && lastSpeedGameTick != 0)
            {
                long realDelta = nowReal - lastSpeedRealTick;
                long gameDelta = nowGame - lastSpeedGameTick;
                if (realDelta > 0 && gameDelta >= 0)
                {
                    double s = (double)gameDelta / realDelta;
                    if (s >= 0 && s < 100000)
                        cachedGameSpeed = (cachedGameSpeed * 0.7) + (s * 0.3);
                }
            }

            lastSpeedRealTick = nowReal;
            lastSpeedGameTick = nowGame;
            return cachedGameSpeed;
        }

        private static string GetCampaignId()
        {
            var career = InjectionFramework.Instance.GetInstance<PlayerCareer>();
            if (career != null && !string.IsNullOrEmpty(career.StartScenarioId))
            {
                string id = $"{career.StartScenarioId}_{career.StartDate.Ticks}";
                Debug.Log($"{LogTag}.GetCampaignId: '{id}'");
                return id;
            }
            return null;
        }

        public static TimeSpan GetEstimatedTimeRemaining(string messageId)
        {
            if (!states.TryGetValue(messageId, out var state) || state.IsDecrypted)
                return TimeSpan.Zero;

            if (!TryGetGameTime())
                return TimeSpan.Zero;

            long nowTicks = gameTime.CurrentDateTime.Ticks;
            long elapsedTicks = nowTicks - state.StartTick;
            long totalGameTicks = state.TotalChars * TicksPerChar;

            if (elapsedTicks >= totalGameTicks)
                return TimeSpan.Zero;

            long remainingGameTicks = totalGameTicks - elapsedTicks;
            double speed = GetGameSpeed();

            if (speed <= 0.001)
                return TimeSpan.MaxValue;

            long remainingRealTicks = (long)(remainingGameTicks / speed);
            return TimeSpan.FromTicks(remainingRealTicks);
        }

        public static void Init()
        {
            if (initialized)
                return;
            initialized = true;
            savePath = Path.Combine(Application.persistentDataPath, "EnigmaMod", "decryption.json");
            Debug.Log($"{LogTag}.Init: savePath='{savePath}'");

            TryGetGameTime();
            Debug.Log($"{LogTag}.Init: campaignId='{GetCampaignId()}'");
            Load();
        }

        public static bool IsDecrypted(string messageId)
        {
            Init();
            if (!states.TryGetValue(messageId, out var s))
            {
                Debug.Log($"{LogTag}.IsDecrypted('{messageId}'): false — no state found");
                return false;
            }
            if (s.IsDecrypted)
            {
                Debug.Log($"{LogTag}.IsDecrypted('{messageId}'): true — already marked decrypted");
                return true;
            }
            GetProgress(messageId);
            Debug.Log($"{LogTag}.IsDecrypted('{messageId}'): {s.IsDecrypted} (rechecked progress)");
            return s.IsDecrypted;
        }

        public static int GetProgress(string messageId)
        {
            Init();
            if (!states.TryGetValue(messageId, out var state))
            {
                Debug.LogWarning($"{LogTag}.GetProgress('{messageId}'): -1 — no state found");
                return -1;
            }
            if (state.IsDecrypted)
            {
                Debug.Log($"{LogTag}.GetProgress('{messageId}'): already decrypted ({state.TotalChars}/{state.TotalChars})");
                return state.TotalChars;
            }

            if (!TryGetGameTime())
            {
                Debug.LogError($"{LogTag}.GetProgress('{messageId}'): GameTime unavailable");
                return -1;
            }

            long nowTicks = gameTime.CurrentDateTime.Ticks;
            long elapsedTicks = nowTicks - state.StartTick;
            int timeBased = Math.Min(state.TotalChars, Math.Max(0, (int)(elapsedTicks / TicksPerChar)));
            int revealed = Math.Max(timeBased, state.RevealedCount);
            revealed = Math.Min(state.TotalChars, revealed);

            Debug.Log($"{LogTag}.GetProgress('{messageId}'): nowTick={nowTicks}, startTick={state.StartTick}, elapsedTicks={elapsedTicks}, ticksPerChar={TicksPerChar}, timeBased={timeBased}, savedRevealed={state.RevealedCount}, revealed={revealed}/{state.TotalChars}");

            if (revealed >= state.TotalChars)
            {
                state.IsDecrypted = true;
                state.RevealedCount = state.TotalChars;
                Debug.Log($"{LogTag}.GetProgress('{messageId}'): COMPLETE — marking as decrypted");
                Save();
                return revealed;
            }

            if (revealed != state.RevealedCount)
            {
                state.RevealedCount = revealed;
                long nowReal = DateTime.UtcNow.Ticks;
                if (nowReal - lastSaveRealTick > SaveIntervalTicks)
                {
                    lastSaveRealTick = nowReal;
                    Save();
                }
            }

            return revealed;
        }

        public static void StartDecryption(string messageId, int totalChars, string ciphertext, string plaintext)
        {
            Init();
            if (states.ContainsKey(messageId))
            {
                Debug.Log($"{LogTag}.StartDecryption('{messageId}'): already exists, skipping (wasDecrypted={states[messageId].IsDecrypted})");
                return;
            }

            if (!TryGetGameTime())
            {
                Debug.LogError($"{LogTag}.StartDecryption('{messageId}'): GameTime unavailable, cannot start decryption");
                return;
            }

            long startTick = gameTime.CurrentDateTime.Ticks;
            Debug.Log($"{LogTag}.StartDecryption('{messageId}'): totalChars={totalChars}, startTick={startTick}, now={gameTime.CurrentDateTime:O}");

            states[messageId] = new DecryptionState
            {
                MessageId = messageId,
                StartTick = startTick,
                TotalChars = totalChars,
                PlaintextLength = plaintext?.Length ?? 0,
                IsDecrypted = false,
                Ciphertext = ciphertext,
                Plaintext = plaintext,
                RevealedCount = 0
            };
            Save();
        }

        public static DecryptionState GetState(string messageId)
        {
            Init();
            states.TryGetValue(messageId, out var state);
            return state;
        }

        public static string GetCiphertext(string messageId)
        {
            Init();
            if (states.TryGetValue(messageId, out var s))
            {
                Debug.Log($"{LogTag}.GetCiphertext('{messageId}'): found, len={s.Ciphertext?.Length ?? 0}");
                return s.Ciphertext;
            }
            Debug.LogWarning($"{LogTag}.GetCiphertext('{messageId}'): not found, returning null");
            return null;
        }

        public static string GetPlaintext(string messageId)
        {
            Init();
            if (states.TryGetValue(messageId, out var s))
            {
                Debug.Log($"{LogTag}.GetPlaintext('{messageId}'): found, len={s.Plaintext?.Length ?? 0}");
                return s.Plaintext;
            }
            Debug.LogWarning($"{LogTag}.GetPlaintext('{messageId}'): not found, returning null");
            return null;
        }

        private static void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(savePath);
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                    Debug.Log($"{LogTag}.Save: created directory '{dir}'");
                }

                string campaignId = GetCampaignId();
                if (campaignId == null)
                {
                    Debug.LogWarning($"{LogTag}.Save: no CampaignId, skipping save");
                    return;
                }

                var data = new DecryptionSaveData
                {
                    CampaignId = campaignId,
                    States = new List<DecryptionState>(states.Values)
                };
                string json = JsonUtility.ToJson(data, true);
                File.WriteAllText(savePath, json);
                Debug.Log($"{LogTag}.Save: saved {states.Count} states (campaign='{campaignId}') ({json.Length} bytes)");
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag}.Save: FAILED — {e.Message}");
            }
        }

        private static void Load()
        {
            try
            {
                if (!File.Exists(savePath))
                {
                    Debug.Log($"{LogTag}.Load: no save file at '{savePath}'");
                    return;
                }

                string json = File.ReadAllText(savePath);
                var data = JsonUtility.FromJson<DecryptionSaveData>(json);
                if (data?.States != null)
                {
                    string storedCampaignId = data.CampaignId;
                    if (string.IsNullOrEmpty(storedCampaignId))
                    {
                        Debug.LogWarning($"{LogTag}.Load: save file has no CampaignId (old version), discarding states");
                        return;
                    }

                    string currentId = GetCampaignId();
                    if (currentId == null || storedCampaignId != currentId)
                    {
                        Debug.Log($"{LogTag}.Load: CampaignId mismatch (stored='{storedCampaignId}', current='{currentId}'), discarding states");
                        return;
                    }

                    states.Clear();
                    foreach (var state in data.States)
                        states[state.MessageId] = state;
                    Debug.Log($"{LogTag}.Load: loaded {states.Count} states for campaign '{storedCampaignId}' ({json.Length} bytes)");
                    foreach (var kv in states)
                        Debug.Log($"{LogTag}.Load:   '{kv.Key}': totalChars={kv.Value.TotalChars}, isDecrypted={kv.Value.IsDecrypted}, startTick={kv.Value.StartTick}, revealed={kv.Value.RevealedCount}");
                }
                else
                {
                    Debug.LogWarning($"{LogTag}.Load: save file exists but data is null or empty (jsonLen={json.Length})");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogTag}.Load: FAILED — {e.Message}");
            }
        }
    }
}
