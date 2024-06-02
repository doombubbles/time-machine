using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using BTD_Mod_Helper.Api.Helpers;
using BTD_Mod_Helper.Api.ModOptions;
using BTD_Mod_Helper.Extensions;
using Il2CppAssets.Scripts.Data.Boss;
using Il2CppAssets.Scripts.Models.Profile;
using Il2CppAssets.Scripts.Models.ServerEvents;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.Bridge;
using Il2CppAssets.Scripts.Unity.UI_New;
using Il2CppAssets.Scripts.Unity.UI_New.DailyChallenge;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using Il2CppNewtonsoft.Json;
using MelonLoader;
using TimeMachine;
using UnityEngine;
using Directory = System.IO.Directory;
using DirectoryInfo = System.IO.DirectoryInfo;
using File = System.IO.File;
using Path = System.IO.Path;
using SearchOption = System.IO.SearchOption;
using TaskScheduler = BTD_Mod_Helper.Api.TaskScheduler;

[assembly: MelonInfo(typeof(TimeMachineMod), ModHelperData.Name, ModHelperData.Version, ModHelperData.RepoOwner)]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]

namespace TimeMachine;

public class TimeMachineMod : BloonsTD6Mod
{
    public const string SavesFolderName = "TimeMachineSaves";

    public static readonly ModSettingButton OpenSavesFolder = new(() => Process.Start(new ProcessStartInfo
    {
        FileName = SavesFolder,
        UseShellExecute = true,
        Verb = "open"
    }))
    {
        buttonText = "Open"
    };

    public static readonly ModSettingButton DeleteData = new(() =>
        PopupScreen.instance.ShowPopup(PopupScreen.Placement.menuCenter, "Delete Data",
            "Are you sure you want to delete all Time Machine Backups?", new Action(ClearData), "Delete", null,
            "Cancel", Popup.TransitionAnim.Scale))

    {
        displayName = "Calculating...",
        buttonText = "Delete Data",
        buttonSprite = VanillaSprites.RedBtnLong,
        modifyOption = CalcSize
    };

    private static ModHelperOption? deleteOption;

    public static string SavesFolder => Path.Combine(FileIOHelper.sandboxRoot, SavesFolderName);
    public static MapSaveDataModel? MapSave { get; private set; }
    public static ReadonlyInGameData? LastInGameData { get; set; }

    public static void CalcSize(ModHelperOption? option = null)
    {
        if (option != null) deleteOption = option;

        var folder = new DirectoryInfo(SavesFolder);
        if (!folder.Exists || deleteOption == null) return;

        Task.Run(() => folder.EnumerateFiles("*", SearchOption.AllDirectories).Sum(info => info.Length))
            .ContinueWith(task => TaskScheduler.ScheduleTask(() =>
            {
                if (deleteOption != null)
                {
                    deleteOption.TopRow.GetComponentInChildren<ModHelperText>()
                        .SetText($"Storing {task.Result / 1000000.0:N1} mb of data");
                }
            }));
    }

    public static void ClearData()
    {
        try
        {
            Directory.Delete(SavesFolder, true);
            Directory.CreateDirectory(SavesFolder);
        }
        catch (Exception e)
        {
            ModHelper.Warning<TimeMachineMod>(e);
        }

        CalcSize();
    }

    /// <summary>
    /// Remove old files for saves that are no longer anywhere in the profile
    /// </summary>
    public override void OnMainMenu()
    {
        var folder = new DirectoryInfo(SavesFolder);

        if (!folder.Exists || MapSave != null) return;

        var allSavedMaps = Game.instance.playerService.Player.Data.AllSavedMaps;

        var usedGameIds = new HashSet<string>();

        foreach (var (_, mapSave) in allSavedMaps)
        {
            usedGameIds.Add(JsonConvert.SerializeObject(mapSave.gameId));
        }

        foreach (var directoryInfo in folder.GetDirectories().ToList()
                     .Where(directoryInfo => !usedGameIds.Contains(directoryInfo.Name)))
        {
            ModHelper.Msg<TimeMachineMod>(
                $"Deleting Time Machine saves for game {directoryInfo.Name} since it was removed from profile");
            try
            {
                directoryInfo.Delete(true);
            }
            catch (Exception e)
            {
                ModHelper.Warning<TimeMachineMod>(e);
            }
        }
    }

    /// <summary>
    /// Load up the save state for the InGame at the given round
    /// </summary>
    /// <param name="round"></param>
    public static void LoadRound(int round)
    {
        if (InGame.instance == null) return;

        if (round <= 1)
        {
            InGame.instance.Restart();
            return;
        }

        var gameId = InGame.instance.GameId;

        var file = FileNameFor(gameId, round);

        if (File.Exists(Path.Combine(FileIOHelper.sandboxRoot, file)))
        {
            var saveModel = FileIOHelper.LoadObject<MapSaveDataModel>(file);
            LoadSave(saveModel);
        }
        else
        {
            ModHelper.Warning<TimeMachineMod>($"No Time Machine data for {gameId}/{InGame.instance.currentRoundId}");
        }
    }

    /// <summary>
    /// Load up SaveModel, works while InGame or not
    /// </summary>
    /// <param name="saveModel"></param>
    public static void LoadSave(MapSaveDataModel saveModel)
    {
        if (InGame.instance != null)
        {
            MapSave = saveModel;
            InGame.instance.Quit();
            return;
        }

        MapSave = null;
        var inGameData = InGameData.Editable;
        inGameData.selectedMode = saveModel.modeName;
        inGameData.selectedMap = saveModel.mapName;
        inGameData.gameType = saveModel.gameType;
        inGameData.selectedDifficulty = saveModel.mapDifficulty;

        if (saveModel.metaData.ContainsKey("BossRounds-BossType") &&
            saveModel.metaData.ContainsKey("BossRounds-IsElite") &&
            Enum.TryParse(saveModel.metaData["BossRounds-BossType"], out BossType bossType) &&
            bool.TryParse(saveModel.metaData["BossRounds-IsElite"], out var isElite))
        {
            inGameData.SetupBoss("BossRoundsMod", bossType, isElite, false,
                BossGameData.DefaultSpawnRounds, new DailyChallengeModel
                {
                    difficulty = saveModel.mapDifficulty,
                    map = saveModel.mapName,
                    mode = saveModel.modeName
                }, LeaderboardScoringType.GameTime);
        }

        if (saveModel.gameType is GameType.BossBloon or GameType.BossChallenge && LastInGameData != null)
        {
            var bossData = LastInGameData.bossData;
            inGameData.SetupBoss(saveModel.dailyChallengeEventID, bossData.bossBloon, bossData.bossEliteMode,
                bossData.bossRankedMode, bossData.spawnRounds, LastInGameData.dcModel, LastInGameData.scoringType);
        }

        UI.instance.LoadGame(null, null, saveModel);
        Game.instance.playerService.Player.Data.SetSavedMap(saveModel.savedMapsId, saveModel);
    }

    public static string FileNameFor(int gameId, int round) =>
        Path.Combine(SavesFolderName, gameId.ToString(), round + ".json");

    /// <summary>
    /// Creates the timeline UI bar on a screen
    /// </summary>
    /// <param name="mainPanel"></param>
    /// <param name="yOffset"></param>
    public static void CreateTimelineUI(GameObject mainPanel, int yOffset = 0)
    {
        var gameId = InGame.instance.GameId;
        var folder = new DirectoryInfo(Path.Combine(FileIOHelper.sandboxRoot, SavesFolderName, gameId.ToString()));

        if (!folder.Exists) return;

        var rounds = folder.GetFiles()
            .Select(fileInfo =>
                int.TryParse(Path.GetFileNameWithoutExtension(fileInfo.Name), out var round) ? round : 0)
            .Where(i => i != 0).OrderBy(i => i).ToList();

        if (!rounds.Any()) return;

        var mainScroll = mainPanel.AddModHelperScrollPanel(
            new Info("TimeMachineScroll", 0, -1150 + yOffset, 2500, 150),
            RectTransform.Axis.Horizontal, VanillaSprites.MainBgPanelHematite, 100);

        mainPanel.AddModHelperComponent(
            ModHelperImage.Create(new Info("TimeIcon", -1400, -1150 + yOffset, 175), VanillaSprites.StopWatch)
        );

        mainPanel.AddModHelperComponent(
            ModHelperImage.Create(new Info("TimeIcon2", 1400, -1150 + yOffset, 200), VanillaSprites.DartTimeIcon)
        );


        var currentRound = InGame.instance.bridge.GetCurrentRound();

        foreach (var round in rounds)
        {
            var message =
                $"Travel back {(round > currentRound ? "(to the future!)" : "to")} when you finished round {round}?\n" +
                $"Round {round + 1} will be about to start.";

            var image = round == currentRound ? VanillaSprites.YellowBtn : VanillaSprites.BrightBlueBtn;

            var btn = mainScroll.ScrollContent.AddButton(
                new Info($"Btn{round}", 140), image,
                new Action(() => PopupScreen.instance.SafelyQueue(screen =>
                    screen.ShowPopup(PopupScreen.Placement.menuCenter, "Time Machine", message,
                        new Action(() => LoadRound(round)), "Yes", null, "No",
                        Popup.TransitionAnim.Scale, PopupScreen.BackGround.Grey))
                )
            );

            btn.AddText(new Info("Text", InfoPreset.FillParent) {Width = 50}, round.ToString(), 100);
        }

        var progress = InGame.instance.bridge.GetCurrentRound() / (float) rounds.Max();
        mainScroll.ScrollRect.horizontalNormalizedPosition = Math.Clamp(progress, 0, 1);
    }
}