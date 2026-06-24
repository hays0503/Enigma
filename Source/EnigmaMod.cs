using HarmonyLib;
using UBOAT.Game;
using UnityEngine;

namespace EnigmaMod
{
    public class EnigmaMod : IUserMod
    {
        public void OnLoaded()
        {
            Debug.Log("[EnigmaMod] OnLoaded() called. Applying Harmony patches...");
            Debug.Log("[EnigmaMod] Assembly: " + GetType().Assembly.FullName);

            Harmony harmony = new Harmony("com.uboat.enigmamod");
            harmony.PatchAll();

            Debug.Log("[EnigmaMod] Harmony.PatchAll() completed.");
        }
    }
}
