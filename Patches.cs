using BTD_Mod_Helper;
using BTD_Mod_Helper.Extensions;
using HarmonyLib;
using Il2CppAssets.Scripts;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Artifacts;
using Il2CppAssets.Scripts.Models.Profile;
using Il2CppAssets.Scripts.Models.Towers;
using Il2CppAssets.Scripts.Simulation;
using Il2CppAssets.Scripts.Simulation.Artifacts;
using Il2CppAssets.Scripts.Simulation.Input;
using Il2CppAssets.Scripts.Simulation.SMath;
using Il2CppAssets.Scripts.Simulation.Towers;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.Bridge;
using Il2CppAssets.Scripts.Unity.UI_New.GameOver;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.InGame.TowerSelectionMenu;
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
/// Restore saved rogue insta monkey cooldowns
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

/// <summary>
/// Timeline UI for frontier defeat screen
/// </summary>
[HarmonyPatch(typeof(FrontierDefeatScreen), nameof(FrontierDefeatScreen.Open))]
internal static class FrontierDefeatScreen_Open
{
    [HarmonyPostfix]
    private static void Postfix(FrontierDefeatScreen __instance)
    {
        TimeMachineMod.CreateTimelineUI(__instance.commonPanel.gameObject, -50);
    }
}

/// <summary>
/// In the real method this happens after the call to base.Initialise, which can cause an error
/// </summary>
[HarmonyPatch(typeof(StaminaTraitArtifact), nameof(StaminaTraitArtifact.Initialise))]
internal static class StaminaTraitArtifact_Initialise
{
    [HarmonyPrefix]
    internal static void Prefix(StaminaTraitArtifact __instance, Model modelToUse)
    {
        if (modelToUse.Is(out StaminaTraitArtifactModel staminaTraitArtifactModel))
        {
            __instance.staminaTraitArtifactModel = staminaTraitArtifactModel;
        }
    }
}

/// <summary>
/// Save the data of frontier insta monkey stamina
/// </summary>
[HarmonyPatch(typeof(Simulation), nameof(Simulation.GetSaveMetaData))]
internal static class Simulation_GetSaveMetaData
{
    [HarmonyPostfix]
    internal static void Postfix(Simulation __instance, Dictionary<string, string> metaData)
    {
        if (__instance.frontierInstaInventory?.instas == null) return;

        foreach (var insta in __instance.frontierInstaInventory.instas)
        {
            metaData["FrontierStamina_" + insta.frontierId] = insta.stamina.ToString();
        }
    }
}

/// <summary>
/// Restore saved frontier insta monkey stamina
/// </summary>
[HarmonyPatch(typeof(Simulation), nameof(Simulation.SetSaveMetaData))]
internal static class Simulation_SetSaveMetaData
{
    [HarmonyPostfix]
    internal static void Postfix(Simulation __instance, Dictionary<string, string> metaData)
    {
        if (__instance.frontierInstaInventory?.instas == null) return;

        foreach (var insta in __instance.frontierInstaInventory.instas)
        {
            if (metaData.TryGetValue("FrontierStamina_" + insta.frontierId, out var s) &&
                float.TryParse(s, out var stamina))
            {
                insta.stamina = stamina;
            }
        }
    }
}

/// <summary>
/// Prevent incidental error after restoring save in frontier match
/// </summary>
[HarmonyPatch(typeof(FrontierTraitsPanel), nameof(FrontierTraitsPanel.UpdateStamina))]
internal static class FrontierTraitsPanel_UpdateStamina
{
    [HarmonyPrefix]
    internal static bool Prefix(FrontierTraitsPanel __instance) => __instance.insta != null;
}

/// <summary>
/// Properly restore frontier Id
/// </summary>
[HarmonyPatch(typeof(TowerManager), nameof(TowerManager.CreateTower))]
internal static class TowerManager_CreateTower
{
    [HarmonyPrefix]
    internal static void Prefix(TowerSaveDataModel? loadingSaveData, ref int frontierId)
    {
        if (loadingSaveData is { frontierId: > -1 })
        {
            frontierId = loadingSaveData.frontierId;
        }
    }
}