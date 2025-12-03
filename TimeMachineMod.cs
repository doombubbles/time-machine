using System;
using System.Diagnostics;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using BTD_Mod_Helper;
using BTD_Mod_Helper.Api.Components;
using BTD_Mod_Helper.Api.Enums;
using BTD_Mod_Helper.Api.Helpers;
using BTD_Mod_Helper.Api.ModOptions;
using BTD_Mod_Helper.Extensions;
using Il2CppAssets.Scripts.Models;
using Il2CppAssets.Scripts.Models.Profile;
using Il2CppAssets.Scripts.Unity;
using Il2CppAssets.Scripts.Unity.Menu;
using Il2CppAssets.Scripts.Unity.UI_New.InGame;
using Il2CppAssets.Scripts.Unity.UI_New.InGame.RightMenu;
using Il2CppAssets.Scripts.Unity.UI_New.Popups;
using Il2CppAssets.Scripts.Utils;
using Il2CppNewtonsoft.Json;
using Il2CppNinjaKiwi.GUTS.Models.ContentBrowser;
using MelonLoader;
using TimeMachine;
using UnityEngine;
using static Il2CppAssets.Scripts.Models.ServerEvents.ChallengeType;
using Directory = System.IO.Directory;
using DirectoryInfo = System.IO.DirectoryInfo;
using File = System.IO.File;
using Path = System.IO.Path;
using FileInfo = System.IO.FileInfo;
using MemoryStream = System.IO.MemoryStream;
using SearchOption = System.IO.SearchOption;
using TaskScheduler = BTD_Mod_Helper.Api.TaskScheduler;

[assembly: MelonInfo(typeof(TimeMachineMod), ModHelperData.Name, ModHelperData.Version, ModHelperData.RepoOwner)]
[assembly: MelonGame("Ninja Kiwi", "BloonsTD6")]

namespace TimeMachine;

public class TimeMachineMod : BloonsTD6Mod
{
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

    public const string SavesFolderName = "TimeMachineSaves";

    public static string OldSavesFolder => Path.Combine(FileIOHelper.sandboxRoot, SavesFolderName);

    public static string SavesFolder =>
        Path.Combine(Game.instance.playerService.configuration.playerDataRootPath, SavesFolderName,
            Game.Player.Data.ownerID ?? "");

    internal static readonly JsonSerializerSettings Settings = new()
    {
        TypeNameHandling = TypeNameHandling.Objects,
    };

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

        if (!folder.Exists && Directory.Exists(OldSavesFolder))
        {
            folder.Parent!.Create();
            Directory.Move(OldSavesFolder, SavesFolder);
        }

        if (!folder.Exists || Game.Player.OnlineData == null && !string.IsNullOrEmpty(Game.Player.Data.ownerID))
            return;

        var allSavedMaps = Game.Player.Data.AllSavedMaps.GetValues().ToArray().OfIl2CppType<MapSaveDataModel>();

        var onlineSaves = Game.Player.OnlineData?.contentBrowserData[ContentType.Map].saveData.ToArray()
            .OfIl2CppType<MapSaveDataModel>();

        var usedGameIds = allSavedMaps.Concat(onlineSaves ?? Array.Empty<MapSaveDataModel>())
            .Select(mapSave => JsonConvert.SerializeObject(mapSave.gameId));

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
    /// Save the state for the InGame at the given round
    /// </summary>
    /// <param name="completedRound"></param>
    /// <param name="highestCompletedRound"></param>
    public static void SaveRound(int completedRound, int highestCompletedRound)
    {
        var path = FilePathFor(CurrentTimeMachineID, completedRound + 1);
        var saveModel = InGame.instance.CreateCurrentMapSave(highestCompletedRound, InGame.instance.MapDataSaveId);
        var text = JsonConvert.SerializeObject(saveModel, Settings);
        var bytes = Encoding.UTF8.GetBytes(text);

        using var outputStream = new MemoryStream(bytes);
        using (var zlibStream = new ZLibStream(outputStream, CompressionMode.Compress))
        {
            zlibStream.Write(bytes, 0, bytes.Length);
        }

        Directory.CreateDirectory(new FileInfo(path).DirectoryName!);
        File.WriteAllBytes(path, outputStream.ToArray());
    }

    /// <summary>
    /// Load up the save state for the InGame at the given round
    /// </summary>
    /// <param name="round"></param>
    public static void LoadRound(int round)
    {
        if (InGame.instance == null) return;

        var file = FilePathFor(CurrentTimeMachineID, round);

        string text;

        if (File.Exists(file))
        {
            var bytes = File.ReadAllBytes(file);
            using var inputStream = new MemoryStream(bytes);
            using var outputStream = new MemoryStream();
            using (var zlibStream = new ZLibStream(inputStream, CompressionMode.Decompress))
            {
                zlibStream.CopyTo(outputStream);
            }
            text = Encoding.UTF8.GetString(outputStream.ToArray());
        }
        else if (File.Exists(file + ".json"))
        {
            text = File.ReadAllText(file + ".json");
        }
        else
        {
            ModHelper.Warning<TimeMachineMod>($"No Time Machine data for {file}");
            return;
        }

        var saveModel = JsonConvert.DeserializeObject<MapSaveDataModel>(text, Settings);

        LoadSave(saveModel);
    }

    /// <summary>
    /// Load up SaveModel
    /// </summary>
    /// <param name="saveModel"></param>
    public static void LoadSave(MapSaveDataModel saveModel)
    {
        if (InGameData.CurrentGame?.dcModel?.chalType is UserPlay or CustomMapPlay &&
            saveModel.gameVersion != Game.Version.ToString())
        {
            PopupScreen.instance.SafelyQueue(screen =>
                screen.ShowOkPopup("Can't load save from an older BTD6 version for a Custom Map"));
            return;
        }

        InGame.Bridge.ExecuteContinueFromCheckpoint(InGame.Bridge.MyPlayerNumber, new KonFuze(), ref saveModel,
            true, false);
        if (InGame.instance.GameType == GameType.Rogue)
        {
            ShopMenu.instance.RebuildRogueTowers();
            foreach (var artifact in InGameData.CurrentGame!.rogueData.equippedArtifacts)
            {
                InGame.Bridge.Simulation.artifactManager.Activate(artifact.artifactName);
            }
        }

        Game.Player.Data.SetSavedMap(saveModel.savedMapsId, saveModel);
    }

    public static string CurrentTimeMachineID => InGame.instance.GameId.ToString();

    public static string FilePathFor(string gameId, int round) =>
        Path.Combine(SavesFolder, gameId, round.ToString());

    /// <summary>
    /// Creates the timeline UI bar on a screen
    /// </summary>
    /// <param name="mainPanel"></param>
    /// <param name="yOffset"></param>
    public static void CreateTimelineUI(GameObject mainPanel, int yOffset = 0)
    {
        var folder = new DirectoryInfo(Path.Combine(SavesFolder, CurrentTimeMachineID));

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
                new Action(() =>
                {
                    MenuManager.instance.buttonClick3Sound.Play("ClickSounds");
                    PopupScreen.instance.SafelyQueue(screen =>
                        screen.ShowPopup(PopupScreen.Placement.menuCenter, "Time Machine", message,
                            new Action(() => LoadRound(round)), "Yes", null, "No",
                            Popup.TransitionAnim.Scale, PopupScreen.BackGround.Grey));
                })
            );

            btn.AddText(new Info("Text", InfoPreset.FillParent) {Width = 50}, round.ToString(), 100);
        }

        var progress = InGame.instance.bridge.GetCurrentRound() / (float) rounds.Max();
        mainScroll.ScrollRect.horizontalNormalizedPosition = Math.Clamp(progress, 0, 1);
    }
}