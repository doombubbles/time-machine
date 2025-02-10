using BTD_Mod_Helper;
using HarmonyLib;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Profile;
using Il2CppAssets.Scripts.Simulation.Input;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.Bridge;
using Il2CppAssets.Scripts.Unity.UI_New.GameOver;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.Legends;
using Il2CppAssets.Scripts.Unity.UI_New.Pause;
using Il2CppAssets.Scripts.Utils;
using Il2CppSystem.Collections.Generic;

namespace TimeMachine;

/// <summary>
/// Save the time machine data after each round
/// </summary>
[HarmonyPatch(typeof(InGame), nameof(InGame.RoundEnd))]
internal static class InGame_RoundEnd
{
    [HarmonyPostfix]
    private static void Postfix(int completedRound, int highestCompletedRound)
    {
        var gameId = InGame.instance.GameId;
        if (gameId == 0)
        {
            ModHelper.Warning<TimeMachineMod>("This save is too old to create Time Machine backups for!");
            return;
        }

        TimeMachineMod.SaveRound(completedRound, highestCompletedRound);
    }
}

/// <summary>
/// Timeline UI for pause screen
/// </summary>
[HarmonyPatch(typeof(PauseScreen), nameof(PauseScreen.Open))]
internal static class PauseScreen_Open
{
    [HarmonyPostfix]
    private static void Postfix(PauseScreen __instance)
    {
        if (InGameData.CurrentGame.IsSandbox || InGameData.CurrentGame.selectedMode == "MapEditor") return;
        var mainPanel = __instance.sidePanel.transform.parent.gameObject;
        TimeMachineMod.CreateTimelineUI(mainPanel);
    }
}

/// <summary>
/// Timeline UI for normal defeat screen
/// </summary>
[HarmonyPatch(typeof(DefeatScreen), nameof(DefeatScreen.Open))]
internal static class DefeatScreen_Open
{
    [HarmonyPostfix]
    private static void Postfix(DefeatScreen __instance)
    {
        TimeMachineMod.CreateTimelineUI(__instance.regularObject, -50);
    }
}

/// <summary>
/// Timeline UI for boss defeat screen
/// </summary>
[HarmonyPatch(typeof(BossDefeatScreen), nameof(BossDefeatScreen.Open))]
internal static class BossDefeatScreen_Open
{
    [HarmonyPostfix]
    private static void Postfix(BossDefeatScreen __instance)
    {
        TimeMachineMod.CreateTimelineUI(__instance.commonPanel.gameObject, -50);
    }
}

/// <summary>
/// Timeline UI for rogue defeat screen
/// </summary>
[HarmonyPatch(typeof(RogueDefeatScreen), nameof(RogueDefeatScreen.Open))]
internal static class RogueDefeatScreen_Open
{
    [HarmonyPostfix]
    private static void Postfix(RogueDefeatScreen __instance)
    {
        TimeMachineMod.CreateTimelineUI(__instance.commonPanel.gameObject, -50);
    }
}

/// <summary>
/// Save the data of rogue insta monkey cooldowns
/// </summary>
[HarmonyPatch(typeof(RogueInstaInventory), nameof(RogueInstaInventory.GetSaveMetaData))]
internal static class RogueInstaInventory_GetSaveMetaData
{
    [HarmonyPostfix]
    internal static void Postfix(RogueInstaInventory __instance, Dictionary<string, string> metaData)
    {
        foreach (var insta in __instance.instas)
        {
            metaData["RogueCooldown_" + insta.uniqueId] = insta.currentCooldown.ToString();
        }
    }
}

/// <summary>
/// Restore saved insta monkey cooldowns
/// </summary>
[HarmonyPatch(typeof(RogueInstaInventory), nameof(RogueInstaInventory.SetSaveMetaData))]
internal static class RogueInstaInventory_SetSaveMetaData
{
    [HarmonyPostfix]
    internal static void Postfix(RogueInstaInventory __instance, Dictionary<string, string> metaData)
    {
        foreach (var insta in __instance.instas)
        {
            if (metaData.TryGetValue("RogueCooldown_" + insta.uniqueId, out var s) &&
                int.TryParse(s, out var cooldown))
            {
                insta.currentCooldown = cooldown;
            }
        }
    }
}