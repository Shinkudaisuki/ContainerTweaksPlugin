using BepInEx;
using BepInEx.Logging;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace ContainerTweaks
{
    [BepInPlugin("com.user.containertweaks", "ContainerTweaks Plugin", "1.1.0")]
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
        private static ConfigEntry<KeyCode> quickTransferKey;

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
            quickTransferKey = config.Bind("Quick Transfer", "QuickTransferKey", KeyCode.LeftControl, "Key used to quickly transfer items between containers.");
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

        [HarmonyPatch(typeof(PlayerCamera), "TryPerformInventoryAction")]
		[HarmonyPrefix]
        private static bool TryPerformInventoryAction_Prefix(PlayerCamera __instance, RaycastResult hit, List<RaycastResult> uiCasts, ref bool __result)
        {
            if (!Input.GetKey(quickTransferKey.Value))
            {
                return true;
            }

            InvButton invButton = hit.gameObject.GetComponent<InvButton>();
            if (invButton == null || !invButton.Overlaps(uiCasts))
            {
                Logger.LogInfo("No valid target button.");
                return true;
            }

            Item dragItem = __instance.dragItem;
            Item targetItem = invButton.GetItem();

            if (dragItem == targetItem || dragItem == null || targetItem == null)
            {
                Logger.LogInfo("Invalid drag or target item.");
                return true;
            }

            Container targetContainer = targetItem.GetComponent<Container>();
            WaterContainerItem targetWaterContainer = targetItem.GetComponent<WaterContainerItem>();
            if (targetContainer == null && targetWaterContainer == null)
            {
                Logger.LogInfo("Target container is null.");
                return true;
            }

            if (targetContainer != null && !targetContainer.CanHoldItem(dragItem))
            {
                Logger.LogInfo("Target container cannot hold the dragged item.");
                return true;
            }

            Container sourceContainer = dragItem.ParentContainer();
            WaterContainerItem sourceWaterContainer = dragItem.GetComponent<WaterContainerItem>();
            if (sourceContainer == null && sourceWaterContainer == null)
            {
                Logger.LogInfo("Source container is null.");
                return true;
            }

            __result = true;
            var transferList = new List<Item> { dragItem };
            // Items in container A -> container B
            if (targetContainer != null && sourceContainer != null && sourceWaterContainer == null)
            {
                foreach (Transform child in sourceContainer.transform)
                {
                    Item childItem = child.GetComponent<Item>();
                    if (childItem != null && childItem != dragItem && childItem.Stats.fullName == dragItem.Stats.fullName)
                    {
                        transferList.Add(childItem);
                    }
                }
            }
            // Water containers in container A -> container B
            else if (targetContainer != null && sourceContainer != null && sourceWaterContainer != null)
            {
                var liquidTypeList = new List<string>();
                foreach (LiquidStack liquid in sourceWaterContainer.stack)
                {
                    liquidTypeList.Add(liquid.liquidId);
                }

                foreach (Transform child in sourceContainer.transform)
                {
                    Item childItem = child.GetComponent<Item>();
                    if (childItem == null || childItem == dragItem)
                    {
                        continue;
                    }
                    WaterContainerItem childWaterContainer = child.GetComponent<WaterContainerItem>();
                    if (childWaterContainer != null)
                    {
                        bool matches = true;
                        foreach (string liquidType in liquidTypeList)
                        {
                            if (!childWaterContainer.HasLiquid(liquidType))
                            {
                                matches = false;
                                break;
                            }
                        }
                        if (matches)
                        {
                            transferList.Add(childItem);
                        }
                    }
                }
            }
            // Water container A -> water container B
            else if (targetWaterContainer != null && sourceWaterContainer != null)
            {
                transferList.Clear();
                if (sourceWaterContainer.CurrentTotal == 0f || targetWaterContainer.SpaceLeft == 0f)
                {
                    Logger.LogInfo("No space to transfer liquids or source container is empty.");
                    return true;
                }

                bool matches = true;
                var sourceLiquidTypeList = new List<string>();
                foreach (LiquidStack liquid in sourceWaterContainer.stack)
                {
                    sourceLiquidTypeList.Add(liquid.liquidId);
                }
                Logger.LogInfo($"Source liquid types: {string.Join(", ", sourceLiquidTypeList)}");
                if (targetWaterContainer.CurrentTotal != 0f)
                {
                    var targetLiquidTypeList = new List<string>();
                    foreach (LiquidStack liquid in targetWaterContainer.stack)
                    {
                        targetLiquidTypeList.Add(liquid.liquidId);
                    }
                    Logger.LogInfo($"Target liquid types: {string.Join(", ", targetLiquidTypeList)}");
                    foreach (string liquidType in sourceLiquidTypeList)
                    {
                        if (!targetLiquidTypeList.Contains(liquidType))
                        {
                            matches = false;
                            Logger.LogInfo($"Target container is missing liquid type: {liquidType}");
                            break;
                        }
                    }
                    foreach (string liquidType in targetLiquidTypeList)
                    {
                        if (!sourceLiquidTypeList.Contains(liquidType))
                        {
                            matches = false;
                            Logger.LogInfo($"Source container is missing liquid type: {liquidType}");
                            break;
                        }
                    }
                }
                
                if (matches)
                {
                    Logger.LogInfo("Transferring liquids between water containers.");
                    float transferAmount = Mathf.Min(sourceWaterContainer.CurrentTotal, targetWaterContainer.SpaceLeft);
                    __instance.body.CombineLiquids(targetWaterContainer, sourceWaterContainer, transferAmount);
                }
                invButton.UpdateGraphic();
            }

            foreach (Item item in transferList)
            {
                sourceContainer.UnloadItem(item);
                targetContainer.LoadItem(item);
            }
            return false;

        }

        
    }
}