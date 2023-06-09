using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.Helpers;
using HarmonyLib;
using Il2CppAssets.Scripts.Unity.Bridge;
using Il2CppAssets.Scripts.Unity.UI_New.GameOver;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.Pause;

namespace TimeMachine;

/// <summary>
/// Save the time machine data after each round
/// </summary>
[HarmonyPatch(typeof(InGame), nameof(InGame.RoundEnd))]
internal static class InGame_RoundEnd
{
    [HarmonyPostfix]
    private static void Postfix(InGame __instance, int completedRound, int highestCompletedRound)
    {
        var gameId = InGame.instance.GameId;
        if (gameId == 0)
        {
            ModHelper.Warning<TimeMachineMod>("This save is too old to create Time Machine backups for!");
            return;
        }

        var path = TimeMachineMod.FileNameFor(gameId, completedRound + 1);
        var saveModel = __instance.CreateCurrentMapSave(highestCompletedRound, __instance.MapDataSaveId);
        FileIOHelper.SaveObject(path, saveModel);
        // ModHelper.Msg<TimeMachineMod>($"Saved to {path}");
    }
}

/// <summary>
/// Hijack returning to the main menu when there's a save we're trying to load
/// </summary>
[HarmonyPatch(typeof(InGame._ReturnToMainMenu_d__194), nameof(InGame._ReturnToMainMenu_d__194.MoveNext))]
internal static class InGame_ReturnToMainMenu
{
    [HarmonyPrefix]
    private static void Prefix(InGame._ReturnToMainMenu_d__194 __instance)
    {
        // ModHelper.Msg<TimeMachineMod>(__instance.__1__state);
        if (__instance.__1__state == 4 && TimeMachineMod.MapSave != null)
        {
            TimeMachineMod.LoadSave(TimeMachineMod.MapSave);
        }

        if (InGameData.CurrentGame?.gameEventId == "BossRoundsMod")
        {
            InGameData.CurrentGame.gameType = GameType.BossBloon;
        }
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