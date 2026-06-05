using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace ContainerTweaks
{
    [BepInPlugin("com.user.containertweaks", "ContainerTweaks Plugin", "1.0.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ManualLogSource Log { get; private set; }

        private void Awake()
        {
            Log = Logger;

            ContainerTweaks.Patch.ContainerTweaksPatcher.Initialize(Log, Config);

            Log.LogInfo("ContainerTweaks loaded, patches applied.");
        }
    }
}

namespace ContainerTweaks.Patch
{
    internal class ContainerTweaksPatcher
    {
        private static ManualLogSource Logger;
        private static float currentScrollY;
        private static float targetScrollY;
        private static Vector2 populateOrigPos;
        private static bool positionsCaptured;

        private static ConfigEntry<int> columnCount;
        private static ConfigEntry<int> visibleRowCount;
        private static ConfigEntry<float> cellSpacing;
        private static ConfigEntry<float> startX;
        private static ConfigEntry<float> startY;
        private static ConfigEntry<float> scrollStep;
        private static ConfigEntry<float> scrollLerpSpeed;
        private static ConfigEntry<float> dragDistanceThreshold;

        private static int ColumnCount { get { return Mathf.Max(1, columnCount.Value); } }
        private static int VisibleRowCount { get { return Mathf.Max(1, visibleRowCount.Value); } }
        private static float CellSpacing { get { return Mathf.Max(1f, cellSpacing.Value); } }
        private static float StartX { get { return startX.Value; } }
        private static float StartY { get { return startY.Value; } }
        private static float ScrollStep { get { return Mathf.Max(1f, scrollStep.Value); } }
        private static float ScrollLerpSpeed { get { return Mathf.Max(1f, scrollLerpSpeed.Value); } }
        private static float DragDistanceThreshold { get { return Mathf.Max(0f, dragDistanceThreshold.Value); } }

        public static void Initialize(ManualLogSource logger, ConfigFile config)
        {
            Logger = logger;
            BindConfig(config);

            Harmony harmony = new Harmony("com.user.containertweaks");
            harmony.PatchAll(typeof(ContainerTweaksPatcher));
        }

        private static void BindConfig(ConfigFile config)
        {
            columnCount = config.Bind("Container Grid", "ColumnCount", 6, "Number of columns in the container grid. Minimum: 1.");
            visibleRowCount = config.Bind("Container Grid", "VisibleRowCount", 5, "Number of visible rows before scrolling is needed. Minimum: 1.");
            cellSpacing = config.Bind("Container Grid", "CellSpacing", 64f, "Spacing in pixels between container item cells. Minimum: 1.");
            startX = config.Bind("Container Grid", "StartX", 34.5f, "Anchored X position of the first container item cell.");
            startY = config.Bind("Container Grid", "StartY", -34.5f, "Anchored Y position of the first container item cell.");

            scrollStep = config.Bind("Scrolling", "ScrollStep", 64f, "Scroll distance per mouse wheel notch. Minimum: 1.");
            scrollLerpSpeed = config.Bind("Scrolling", "ScrollLerpSpeed", 20f, "Smoothing speed used when scrolling. Minimum: 1.");

            dragDistanceThreshold = config.Bind("Dragging", "DragDistanceThreshold", 1000f, "Replaces the game's drag distance threshold. Set to 600 to restore the original value.");
        }

        [HarmonyPatch(typeof(PlayerCamera), "OpenContainer")]
        [HarmonyPostfix]
        private static void OpenContainer_Postfix(PlayerCamera __instance)
        {
            if (!positionsCaptured)
            {
                populateOrigPos = __instance.contPopulate.GetComponent<RectTransform>().anchoredPosition;

                positionsCaptured = true;
            }

            currentScrollY = 0f;
            targetScrollY = 0f;
            ApplyScroll(__instance);
        }

        [HarmonyPatch(typeof(PlayerCamera), "Update")]
        [HarmonyPostfix]
        private static void Update_Postfix(PlayerCamera __instance)
        {
            if (!__instance.containerMenu.activeSelf || __instance.currentContainer == null)
            {
                return;
            }

            if (Input.mousePosition.x > (float)Screen.width * 0.5f)
            {
                float scrollDelta = Input.mouseScrollDelta.y * -1f;
                if (Mathf.Abs(scrollDelta) > 0.01f)
                {
                    targetScrollY += scrollDelta * ScrollStep;
                }
            }

            int rowCount = Mathf.CeilToInt((float)__instance.currentContainer.itemCount / (float)ColumnCount);
            float max = Mathf.Max(0f, (float)(rowCount - VisibleRowCount) * CellSpacing);
            targetScrollY = Mathf.Clamp(targetScrollY, 0f, max);

            if (Mathf.Abs(currentScrollY - targetScrollY) > 0.01f)
            {
                currentScrollY = Mathf.Lerp(currentScrollY, targetScrollY, Time.unscaledDeltaTime * ScrollLerpSpeed);
                ApplyScroll(__instance);
            }
        }

        [HarmonyPatch(typeof(PlayerCamera), "HandleWhileDragging")]
        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> HandleWhileDragging_Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var codeMatcher = new CodeMatcher(instructions);
            codeMatcher.MatchStartForward(
                    new CodeMatch(OpCodes.Ldc_R4, 600f))
                .SetAndAdvance(OpCodes.Ldc_R4, DragDistanceThreshold);
            return codeMatcher.InstructionEnumeration();
        }

        [HarmonyPatch(typeof(PlayerCamera), "RepopulateContainer")]
        [HarmonyPostfix]
        private static void RepopulateContainer_Postfix(PlayerCamera __instance)
        {
            if (__instance.currentContainer == null)
            {
                return;
            }

            ReflowContainerGrid(__instance);

            int rowCount = Mathf.CeilToInt((float)__instance.currentContainer.itemCount / (float)ColumnCount);
            float maxScroll = Mathf.Max(0f, (float)(rowCount - VisibleRowCount) * CellSpacing);
            if (targetScrollY > maxScroll)
            {
                targetScrollY = maxScroll;
            }

            ApplyScroll(__instance);
        }

        private static void ApplyScroll(PlayerCamera cam)
        {
            if (!positionsCaptured)
            {
                return;
            }

            cam.contPopulate.GetComponent<RectTransform>().anchoredPosition = populateOrigPos + new Vector2(0f, Mathf.Round(currentScrollY));
        }

        private static void ReflowContainerGrid(PlayerCamera cam)
        {
            if (!positionsCaptured || cam.containerUnloadButtons == null)
            {
                return;
            }

            for (int i = 0; i < cam.containerUnloadButtons.Count; i++)
            {
                GameObject gameObject = cam.containerUnloadButtons[i];
                if (!gameObject)
                {
                    continue;
                }

                RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
                if (!rectTransform)
                {
                    continue;
                }

                int row = i / ColumnCount;
                int column = i % ColumnCount;
                rectTransform.anchoredPosition = new Vector2(StartX + (float)column * CellSpacing, StartY - (float)row * CellSpacing);
            }
        }
    }
}