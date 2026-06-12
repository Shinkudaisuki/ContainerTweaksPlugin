using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ContainerTweaks
{
    [BepInPlugin("com.user.containertweaks", "ContainerTweaks Plugin", "1.3.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log { get; private set; }

        private void Awake()
        {
            Log = Logger;

            ContainerTweaks.Patch.ContainerTweaksPatcher.Initialize(Log, Config);
            ContainerTweaks.Patch.MpCompat.Initialize(Log);

            Log.LogInfo("ContainerTweaks loaded, patches applied.");
        }
    }
}

namespace ContainerTweaks.Patch
{
    internal partial class ContainerTweaksPatcher
    {
        private static ManualLogSource Logger;
        private static float currentScrollY;
        private static float targetScrollY;
        private static Vector2 populateOrigPos;
        private static bool positionsCaptured;

        private static ConfigEntry<bool> containerViewExpansionEnabled;
        private static ConfigEntry<bool> containerScrollingEnabled;
        private static ConfigEntry<bool> quickTransferEnabled;
        private static ConfigEntry<bool> quickTransferItemsEnabled;
        private static ConfigEntry<bool> quickTransferLiquidsEnabled;
        private static ConfigEntry<bool> quickTransferMagazinesEnabled;
        private static ConfigEntry<bool> quickTransferShotgunRoundsEnabled;
        private static ConfigEntry<bool> quickTransferShotgunBoxesEnabled;
        private static ConfigEntry<bool> quickLoadMagazineEnabled;
        private static ConfigEntry<int> columnCount;
        private static ConfigEntry<int> visibleRowCount;
        private static ConfigEntry<float> cellSpacing;
        private static ConfigEntry<float> startX;
        private static ConfigEntry<float> startY;
        private static ConfigEntry<float> scrollStep;
        private static ConfigEntry<float> scrollLerpSpeed;
        private static ConfigEntry<float> dragDistanceThreshold;
        private static ConfigEntry<KeyCode> quickTransferKey;

        private static bool IsContainerViewExpansionEnabled { get { return containerViewExpansionEnabled.Value; } }
        private static bool IsContainerScrollingEnabled { get { return containerScrollingEnabled.Value; } }
        private static bool IsQuickTransferEnabled { get { return quickTransferEnabled.Value; } }
        private static bool IsQuickTransferItemsEnabled { get { return quickTransferItemsEnabled.Value; } }
        private static bool IsQuickTransferLiquidsEnabled { get { return quickTransferLiquidsEnabled.Value; } }
        private static bool IsQuickTransferMagazinesEnabled { get { return quickTransferMagazinesEnabled.Value; } }
        private static bool IsQuickTransferShotgunRoundsEnabled { get { return quickTransferShotgunRoundsEnabled.Value; } }
        private static bool IsQuickTransferShotgunBoxesEnabled { get { return quickTransferShotgunBoxesEnabled.Value; } }
        private static bool IsQuickLoadMagazineEnabled { get { return quickLoadMagazineEnabled.Value; } }
        private static int ColumnCount { get { return Mathf.Max(1, columnCount.Value); } }
        private static int VisibleRowCount { get { return Mathf.Max(1, visibleRowCount.Value); } }
        private static float CellSpacing { get { return Mathf.Max(1f, cellSpacing.Value); } }
        private static float StartX { get { return startX.Value; } }
        private static float StartY { get { return startY.Value; } }
        private static float ScrollStep { get { return Mathf.Max(1f, scrollStep.Value); } }
        private static float ScrollLerpSpeed { get { return Mathf.Max(1f, scrollLerpSpeed.Value); } }
        private static float DragDistanceThreshold { get { return Mathf.Max(0f, dragDistanceThreshold.Value); } }

        internal enum TransferType
        {
            None,
            Item,
            Liquid,
            Magazine,
            ShotgunRound,
            ShotgunBox
        }

        public static void Initialize(ManualLogSource logger, ConfigFile config)
        {
            Logger = logger;
            BindConfig(config);

            Harmony harmony = new Harmony("com.user.containertweaks");
            harmony.PatchAll(typeof(ContainerTweaksPatcher));
        }

        private static void BindConfig(ConfigFile config)
        {
            containerViewExpansionEnabled = config.Bind("Feature Toggles", "ContainerViewExpansionEnabled", true, "Enable expanded container grid layout.");
            containerScrollingEnabled = config.Bind("Feature Toggles", "ContainerScrollingEnabled", true, "Enable mouse wheel scrolling in container views.");
            quickTransferEnabled = config.Bind("Feature Toggles", "QuickTransferEnabled", true, "Enable all quick transfer features.");

            columnCount = config.Bind("Container Grid", "ColumnCount", 6, "Number of columns in the container grid. Minimum: 1.");
            visibleRowCount = config.Bind("Container Grid", "VisibleRowCount", 5, "Number of visible rows before scrolling is needed. Minimum: 1.");
            cellSpacing = config.Bind("Container Grid", "CellSpacing", 64f, "Spacing in pixels between container item cells. Minimum: 1.");
            startX = config.Bind("Container Grid", "StartX", 34.5f, "Anchored X position of the first container item cell.");
            startY = config.Bind("Container Grid", "StartY", -34.5f, "Anchored Y position of the first container item cell.");

            scrollStep = config.Bind("Scrolling", "ScrollStep", 64f, "Scroll distance per mouse wheel notch. Minimum: 1.");
            scrollLerpSpeed = config.Bind("Scrolling", "ScrollLerpSpeed", 20f, "Smoothing speed used when scrolling. Minimum: 1.");

            dragDistanceThreshold = config.Bind("Dragging", "DragDistanceThreshold", 1000f, "Replaces the game's drag distance threshold. Set to 600 to restore the original value.");
            quickTransferKey = config.Bind("Quick Transfer", "QuickTransferKey", KeyCode.LeftControl, "Key used to quickly transfer items between containers.");
            quickTransferItemsEnabled = config.Bind("Quick Transfer", "ItemTransferEnabled", true, "Enable moving matching items into compatible target containers.");
            quickTransferLiquidsEnabled = config.Bind("Quick Transfer", "LiquidTransferEnabled", true, "Enable transferring liquid between compatible liquid containers.");
            quickTransferMagazinesEnabled = config.Bind("Quick Transfer", "MagazineTransferEnabled", true, "Enable moving rounds between magazines of the same ammo type.");
            quickTransferShotgunRoundsEnabled = config.Bind("Quick Transfer", "ShotgunRoundLoadEnabled", true, "Enable loading shotgun rounds from the source container into a shotgun.");
            quickTransferShotgunBoxesEnabled = config.Bind("Quick Transfer", "ShotgunBoxLoadEnabled", true, "Enable loading shotgun shells from shotgun ammo boxes into a shotgun.");
            quickLoadMagazineEnabled = config.Bind("Quick Transfer", "MagazineQuickLoadEnabled", true, "Enable quick loading matching loose rounds into magazines.");
        }
    }
}