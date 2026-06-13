using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ContainerTweaks
{
    [BepInPlugin("com.user.containertweaks", "ContainerTweaks Plugin", "1.4.0")]
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
        private static ConfigEntry<bool> quickTransferDirectFeedRoundsEnabled;
        private static ConfigEntry<bool> quickTransferDirectFeedMagazinesEnabled;
        private static ConfigEntry<bool> quickLoadMagazineEnabled;
        private static ConfigEntry<bool> autoHighlightEnabled;
        private static ConfigEntry<bool> autoHighlightWaterContainerEnabled;
        private static ConfigEntry<bool> autoHighlightAmmoEnabled;
        private static ConfigEntry<bool> autoHighlightGunEnabled;
        private static ConfigEntry<string> highlightColorCompatible;
        private static ConfigEntry<string> highlightColorFull;
        private static ConfigEntry<string> highlightColorPartial;
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
        private static bool IsQuickTransferDirectFeedRoundsEnabled { get { return quickTransferDirectFeedRoundsEnabled.Value; } }
        private static bool IsQuickTransferDirectFeedMagazinesEnabled { get { return quickTransferDirectFeedMagazinesEnabled.Value; } }
        private static bool IsQuickLoadMagazineEnabled { get { return quickLoadMagazineEnabled.Value; } }
        private static bool IsAutoHighlightEnabled { get { return autoHighlightEnabled.Value; } }
        private static bool IsAutoHighlightWaterContainerEnabled { get { return autoHighlightWaterContainerEnabled.Value; } }
        private static bool IsAutoHighlightAmmoEnabled { get { return autoHighlightAmmoEnabled.Value; } }
        private static bool IsAutoHighlightGunEnabled { get { return autoHighlightGunEnabled.Value; } }
        private static Color HighlightColorCompatible { get { return ParseColor(highlightColorCompatible.Value, Color.green); } }
        private static Color HighlightColorFull { get { return ParseColor(highlightColorFull.Value, Color.cyan); } }
        private static Color HighlightColorPartial { get { return ParseColor(highlightColorPartial.Value, Color.yellow); } }
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
            DirectFeedRound,
            DirectFeedMagazine
        }

        private static Color ParseColor(string value, Color fallback)
        {
            var parts = value.Split(',');
            if (parts.Length == 4 &&
                float.TryParse(parts[0], out float r) &&
                float.TryParse(parts[1], out float g) &&
                float.TryParse(parts[2], out float b) &&
                float.TryParse(parts[3], out float a))
                return new Color(r, g, b, a);
            return fallback;
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
            quickTransferDirectFeedRoundsEnabled = config.Bind("Quick Transfer", "DirectFeedRoundLoadEnabled", true, "Enable loading rounds from the source container into a direct-feed gun.");
            quickTransferDirectFeedMagazinesEnabled = config.Bind("Quick Transfer", "DirectFeedMagazineLoadEnabled", true, "Enable loading rounds from a magazine into a direct-feed gun.");
            quickLoadMagazineEnabled = config.Bind("Quick Transfer", "MagazineQuickLoadEnabled", true, "Enable quick loading matching loose rounds into magazines.");

            autoHighlightEnabled = config.Bind("Auto Highlight", "AutoHighlightEnabled", true, "Enable auto-highlighting compatible items when dragging.");
            autoHighlightWaterContainerEnabled = config.Bind("Auto Highlight", "WaterContainerHighlightEnabled", true, "Enable highlighting compatible water containers.");
            autoHighlightAmmoEnabled = config.Bind("Auto Highlight", "AmmoHighlightEnabled", true, "Enable highlighting compatible magazines and rounds.");
            autoHighlightGunEnabled = config.Bind("Auto Highlight", "GunHighlightEnabled", true, "Enable highlighting compatible guns.");
            highlightColorCompatible = config.Bind("Auto Highlight", "ColorCompatible", "0,1,0,1", "RGBA color for compatible/loadable items (R,G,B,A). Default: green.");
            highlightColorFull = config.Bind("Auto Highlight", "ColorFull", "0,1,1,1", "RGBA color for compatible but full items (R,G,B,A). Default: cyan.");
            highlightColorPartial = config.Bind("Auto Highlight", "ColorPartial", "1,1,0,1", "RGBA color for partially compatible items (R,G,B,A). Default: yellow.");
        }
    }
}