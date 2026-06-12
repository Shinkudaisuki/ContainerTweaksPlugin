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
    [BepInPlugin("com.user.containertweaks", "ContainerTweaks Plugin", "1.2.0")]
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

        enum TransferType
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
            if (!ShouldHandleQuickTransfer())
            {
                return true;
            }

            InvButton invButton = hit.gameObject.GetComponent<InvButton>();
            if (!TryGetValidTransferContext(__instance, invButton, uiCasts, out TransferType transferType, out Item dragItem, out Item targetItem, out Container sourceContainer, out Container targetContainer, out WaterContainerItem sourceWaterContainer, out WaterContainerItem targetWaterContainer, out AmmoScript dragAmmo, out AmmoScript targetAmmo, out GunScript targetGun))
            {
                return true;
            }

            __result = true;

            switch (transferType)
            {
                case TransferType.None:
                    return true;
                case TransferType.Item:
                {
                    List<Item> transferList = BuildTransferList(dragItem, sourceContainer, targetContainer, sourceWaterContainer, targetWaterContainer);
                    TransferItems(sourceContainer, targetContainer, transferList);
                    return false;
                }
                case TransferType.Liquid:
                    TryTransferBetweenWaterContainers(__instance, invButton, sourceWaterContainer, targetWaterContainer);
                    return false;
                case TransferType.Magazine:
                    TryTransferBetweenMagazines(dragAmmo, targetAmmo);
                    return false;
                case TransferType.ShotgunRound:
                    TryTransfer12gaugeRounds(sourceContainer, targetGun);
                    return false;
                case TransferType.ShotgunBox:
                    TryTransfer12gaugeBox(dragAmmo, targetGun);
                    return false;
            }

            return false;

        }

        private static bool ShouldHandleQuickTransfer()
        {
            return Input.GetKey(quickTransferKey.Value);
        }

        private static bool TryGetValidTransferContext(PlayerCamera camera, InvButton invButton, List<RaycastResult> uiCasts, 
            out TransferType transferType,
            out Item dragItem, out Item targetItem, out Container sourceContainer, out Container targetContainer, 
            out WaterContainerItem sourceWaterContainer, out WaterContainerItem targetWaterContainer, 
            out AmmoScript dragAmmo, out AmmoScript targetAmmo, out GunScript targetGun)
        {
            transferType = TransferType.None;
            dragItem = null;
            targetItem = null;
            sourceContainer = null;
            targetContainer = null;
            sourceWaterContainer = null;
            targetWaterContainer = null;
            dragAmmo = null;
            targetAmmo = null;
            targetGun = null;

            if (invButton == null || !invButton.Overlaps(uiCasts))
            {
                return false;
            }

            dragItem = camera.dragItem;
            targetItem = invButton.GetItem();
            if (dragItem == null || targetItem == null || dragItem == targetItem)
            {
                return false;
            }

            dragAmmo = dragItem.GetComponent<AmmoScript>();
            targetAmmo = targetItem.GetComponent<AmmoScript>();
            if (dragAmmo != null && targetAmmo != null && dragAmmo.itemType == AmmoScript.AmmoItemType.Magazine && targetAmmo.itemType == AmmoScript.AmmoItemType.Magazine && dragAmmo.ammoType == targetAmmo.ammoType)
            {
                transferType = TransferType.Magazine;
            }

            targetContainer = targetItem.GetComponent<Container>();
            targetWaterContainer = targetItem.GetComponent<WaterContainerItem>();
            sourceContainer = dragItem.ParentContainer();
            sourceWaterContainer = dragItem.GetComponent<WaterContainerItem>();
            if (targetContainer != null && sourceContainer != null && targetContainer.CanHoldItem(dragItem))
            {
                transferType = TransferType.Item;
            }
            if (targetWaterContainer != null && sourceWaterContainer != null)
            {
                transferType = TransferType.Liquid;
            }

            targetGun = targetItem.GetComponent<GunScript>();
            if (targetGun != null && dragAmmo != null && sourceContainer != null && dragAmmo.ammoType == GunScript.AmmoType.Shotgun && 
                dragAmmo.itemType == AmmoScript.AmmoItemType.Round && targetGun.ammoType == GunScript.AmmoType.Shotgun)
            {
                transferType = TransferType.ShotgunRound;
            }
            if (targetGun != null && dragAmmo != null && dragAmmo.ammoType == GunScript.AmmoType.Shotgun && 
                dragAmmo.itemType == AmmoScript.AmmoItemType.Magazine && targetGun.ammoType == GunScript.AmmoType.Shotgun)
            {
                transferType = TransferType.ShotgunBox;
            }

            return transferType != TransferType.None;
        }

        private static List<Item> BuildTransferList(Item dragItem, Container sourceContainer, Container targetContainer, WaterContainerItem sourceWaterContainer, WaterContainerItem targetWaterContainer)
        {
            var transferList = new List<Item> { dragItem };

            if (targetContainer != null && sourceContainer != null && sourceWaterContainer == null)
            {
                AddMatchingItemsByName(transferList, sourceContainer, dragItem);
            }
            else if (targetContainer != null && sourceContainer != null && sourceWaterContainer != null)
            {
                AddMatchingWaterContainers(transferList, sourceContainer, dragItem, sourceWaterContainer);
            }

            return transferList;
        }

        private static void AddMatchingItemsByName(List<Item> transferList, Container sourceContainer, Item dragItem)
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

        private static void AddMatchingWaterContainers(List<Item> transferList, Container sourceContainer, Item dragItem, WaterContainerItem sourceWaterContainer)
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
                if (childWaterContainer != null && HasAllLiquids(childWaterContainer, liquidTypeList))
                {
                    transferList.Add(childItem);
                }
            }
        }

        private static bool HasAllLiquids(WaterContainerItem waterContainer, List<string> liquidTypeList)
        {
            foreach (string liquidType in liquidTypeList)
            {
                if (!waterContainer.HasLiquid(liquidType))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryTransferBetweenWaterContainers(PlayerCamera camera, InvButton invButton, WaterContainerItem sourceWaterContainer, WaterContainerItem targetWaterContainer)
        {
            if (sourceWaterContainer == null || targetWaterContainer == null)
            {
                return false;
            }

            if (sourceWaterContainer.CurrentTotal == 0f || targetWaterContainer.SpaceLeft == 0f)
            {
                return true;
            }

            if (!CanCombineLiquids(sourceWaterContainer, targetWaterContainer))
            {
                invButton.UpdateGraphic();
                return true;
            }

            float transferAmount = Mathf.Min(sourceWaterContainer.CurrentTotal, targetWaterContainer.SpaceLeft);
            camera.body.CombineLiquids(targetWaterContainer, sourceWaterContainer, transferAmount);
            invButton.UpdateGraphic();
            return true;
        }

        private static bool CanCombineLiquids(WaterContainerItem sourceWaterContainer, WaterContainerItem targetWaterContainer)
        {
            var sourceLiquidTypeList = GetLiquidTypeList(sourceWaterContainer);
            if (targetWaterContainer.CurrentTotal == 0f)
            {
                return true;
            }

            var targetLiquidTypeList = GetLiquidTypeList(targetWaterContainer);
            return ContainsSameLiquids(sourceLiquidTypeList, targetLiquidTypeList);
        }

        private static List<string> GetLiquidTypeList(WaterContainerItem waterContainer)
        {
            var liquidTypeList = new List<string>();
            foreach (LiquidStack liquid in waterContainer.stack)
            {
                liquidTypeList.Add(liquid.liquidId);
            }

            return liquidTypeList;
        }

        private static bool ContainsSameLiquids(List<string> sourceLiquidTypeList, List<string> targetLiquidTypeList)
        {
            foreach (string liquidType in sourceLiquidTypeList)
            {
                if (!targetLiquidTypeList.Contains(liquidType))
                {
                    return false;
                }
            }

            foreach (string liquidType in targetLiquidTypeList)
            {
                if (!sourceLiquidTypeList.Contains(liquidType))
                {
                    return false;
                }
            }

            return true;
        }

        private static void TransferItems(Container sourceContainer, Container targetContainer, List<Item> transferList)
        {
            if (sourceContainer == null || targetContainer == null)
            {
                return;
            }

            foreach (Item item in transferList)
            {
                sourceContainer.UnloadItem(item);
                targetContainer.LoadItem(item);
            }
        }

        private static bool TryTransferBetweenMagazines(AmmoScript sourceMagazine, AmmoScript targetMagazine)
        {
            if (sourceMagazine == null || targetMagazine == null)
            {
                return false;
            }

            if (sourceMagazine.ammoType != targetMagazine.ammoType || sourceMagazine.itemType != AmmoScript.AmmoItemType.Magazine || targetMagazine.itemType != AmmoScript.AmmoItemType.Magazine)
            {
                return false;
            }

            int transferAmount = Mathf.Min(sourceMagazine.rounds, targetMagazine.maxRounds - targetMagazine.rounds);
            if (transferAmount <= 0)
            {
                return true;
            }

            sourceMagazine.rounds -= transferAmount;
            targetMagazine.rounds += transferAmount;
            Sound.Play("gunloadshell", targetMagazine.transform.position, false, true, null, 1f, 1f, false, false);
            return true;
        }

        private static bool TryTransfer12gaugeRounds(Container sourceContainer, GunScript targetGun)
        {
            foreach (Transform child in sourceContainer.transform)
            {
                if (targetGun.roundsInMag >= targetGun.magCapacity)
                {
                    break;
                }
                AmmoScript childAmmo = child.GetComponent<AmmoScript>();
                if (childAmmo != null && childAmmo.itemType == AmmoScript.AmmoItemType.Round && childAmmo.ammoType == GunScript.AmmoType.Shotgun)
                {
                    if (targetGun.racked && targetGun.roundInChamber == GunScript.RoundInChamber.None)
                    {
                        targetGun.roundInChamber = GunScript.RoundInChamber.Round;
                    }
                    else
                    {
                        targetGun.roundsInMag++;
                    }
                    Object.Destroy(childAmmo.gameObject);
                }
            }
            Sound.Play("gunloadshell", targetGun.transform.position, false, true, null, 1f, 1f, false, false);
            return true;
        }

        private static bool TryTransfer12gaugeBox(AmmoScript sourceBox, GunScript targetGun)
        {
            int roundsToLoad = Mathf.Min(sourceBox.rounds, targetGun.magCapacity - targetGun.roundsInMag);
            if (roundsToLoad <= 0)
            {
                return true;
            }
            sourceBox.rounds -= roundsToLoad;
            if (targetGun.racked && targetGun.roundInChamber == GunScript.RoundInChamber.None)
            {
                targetGun.roundInChamber = GunScript.RoundInChamber.Round;
                roundsToLoad--;
            }
            targetGun.roundsInMag += roundsToLoad;
            Sound.Play("gunloadshell", targetGun.transform.position, false, true, null, 1f, 1f, false, false);
            return true;
        }

        [HarmonyPatch(typeof(AmmoScript), "LoadRound")]
        [HarmonyPrefix]
        private static bool LoadRound_Prefix(AmmoScript __instance, AmmoScript ammo)
        {
            if (!Input.GetKey(quickTransferKey.Value))
            {
                return true;
            }

            if (ammo.itemType == AmmoScript.AmmoItemType.Round && __instance.itemType == AmmoScript.AmmoItemType.Magazine && ammo.ammoType == __instance.ammoType && __instance.rounds < __instance.maxRounds)
            {
                var ammoList = new List<AmmoScript> { ammo };
                Item sourceItem = ammo.GetComponent<Item>();
                if (sourceItem == null)
                {
                    Logger.LogInfo("Source item is null.");
                    return true;
                }
                Container sourceContainer = sourceItem.ParentContainer();
                if (sourceContainer == null)
                {
                    Logger.LogInfo("Source container is null.");
                    return true;
                }
                foreach (Transform child in sourceContainer.transform)
                {
                    AmmoScript childAmmo = child.GetComponent<AmmoScript>();
                    if (childAmmo != null && childAmmo != ammo && childAmmo.itemType == AmmoScript.AmmoItemType.Round && childAmmo.ammoType == ammo.ammoType)
                    {
                        ammoList.Add(childAmmo);
                    }
                }
                foreach (AmmoScript ammoScript in ammoList)
                {
                    if (__instance.rounds >= __instance.maxRounds)
                    {
                        break;
                    }
                    __instance.rounds++;
                    Object.Destroy(ammoScript.gameObject);
                }
                Sound.Play("gunloadshell", __instance.transform.position, false, true, null, 1f, 1f, false, false);
                return false;
            }

            return true;
        }
    }
}