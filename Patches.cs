using BTD_Mod_Helper;
using HarmonyLib;
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