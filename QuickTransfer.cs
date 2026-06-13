using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;

namespace ContainerTweaks.Patch
{
    internal partial class ContainerTweaksPatcher
    {
        public static bool ExecuteQuickTransfer(Body body, TransferType transferType, Item dragItem, Item targetItem, bool networkAware)
        {
            if (body == null || dragItem == null || targetItem == null || transferType == TransferType.None || !IsQuickTransferEnabled || !IsTransferTypeEnabled(transferType))
            {
                return false;
            }

            Container sourceContainer = dragItem.ParentContainer();
            Container targetContainer = targetItem.GetComponent<Container>();
            WaterContainerItem sourceWaterContainer = dragItem.GetComponent<WaterContainerItem>();
            WaterContainerItem targetWaterContainer = targetItem.GetComponent<WaterContainerItem>();
            AmmoScript dragAmmo = dragItem.GetComponent<AmmoScript>();
            AmmoScript targetAmmo = targetItem.GetComponent<AmmoScript>();
            GunScript targetGun = targetItem.GetComponent<GunScript>();

            using (MpCompat.BeginInventoryMutation(body))
            {
                switch (transferType)
                {
                    case TransferType.Item:
                    {
                        if (targetContainer == null || sourceContainer == null || !targetContainer.CanHoldItem(dragItem))
                        {
                            return false;
                        }

                        List<Item> transferList = BuildTransferList(dragItem, sourceContainer, targetContainer, sourceWaterContainer, targetWaterContainer);
                        TransferItems(sourceContainer, targetContainer, transferList);
                        if (networkAware)
                        {
                            foreach (Item item in transferList)
                            {
                                MpCompat.MarkItemChanged(item, true);
                            }
                        }
                        return true;
                    }
                    case TransferType.Liquid:
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
                            return true;
                        }

                        float transferAmount = Mathf.Min(sourceWaterContainer.CurrentTotal, targetWaterContainer.SpaceLeft);
                        body.CombineLiquids(targetWaterContainer, sourceWaterContainer, transferAmount);
                        if (networkAware)
                        {
                            MpCompat.MarkItemChanged(sourceWaterContainer.GetComponent<Item>(), true);
                            MpCompat.MarkItemChanged(targetWaterContainer.GetComponent<Item>(), true);
                        }
                        return true;
                    }
                    case TransferType.Magazine:
                    {
                        if (dragAmmo == null || targetAmmo == null)
                        {
                            return false;
                        }

                        if (dragAmmo.ammoType != targetAmmo.ammoType || dragAmmo.itemType != AmmoScript.AmmoItemType.Magazine || targetAmmo.itemType != AmmoScript.AmmoItemType.Magazine)
                        {
                            return false;
                        }

                        int transferAmount = Mathf.Min(dragAmmo.rounds, targetAmmo.maxRounds - targetAmmo.rounds);
                        if (transferAmount <= 0)
                        {
                            return true;
                        }

                        dragAmmo.rounds -= transferAmount;
                        targetAmmo.rounds += transferAmount;
                        Sound.Play("gunloadshell", targetAmmo.transform.position, false, true, null, 1f, 1f, false, false);
                        if (networkAware)
                        {
                            MpCompat.MarkItemChanged(dragAmmo.GetComponent<Item>(), true);
                            MpCompat.MarkItemChanged(targetAmmo.GetComponent<Item>(), true);
                        }
                        return true;
                    }
                    case TransferType.DirectFeedRound:
                    {
                        if (sourceContainer == null || targetGun == null || targetGun.feedType != GunScript.FeedType.Direct)
                        {
                            return false;
                        }

                        bool loadedAny = false;
                        foreach (Transform child in sourceContainer.transform)
                        {
                            if (targetGun.roundsInMag >= targetGun.magCapacity)
                            {
                                break;
                            }

                            AmmoScript childAmmo = child.GetComponent<AmmoScript>();
                            if (childAmmo != null && childAmmo.itemType == AmmoScript.AmmoItemType.Round && childAmmo.ammoType == targetGun.ammoType)
                            {
                                if (targetGun.racked && targetGun.roundInChamber == GunScript.RoundInChamber.None)
                                {
                                    targetGun.roundInChamber = GunScript.RoundInChamber.Round;
                                }
                                else
                                {
                                    targetGun.roundsInMag++;
                                }

                                if (networkAware)
                                {
                                    MpCompat.MarkItemChanged(childAmmo.GetComponent<Item>(), true);
                                }
                                Object.Destroy(childAmmo.gameObject);
                                loadedAny = true;
                            }
                        }

                        if (loadedAny)
                        {
                            Sound.Play("gunloadshell", targetGun.transform.position, false, true, null, 1f, 1f, false, false);
                            if (networkAware)
                            {
                                MpCompat.MarkItemChanged(targetGun.GetComponent<Item>(), true);
                            }
                        }

                        return true;
                    }
                    case TransferType.DirectFeedMagazine:
                    {
                        if (dragAmmo == null || targetGun == null)
                        {
                            return false;
                        }

                        if (targetGun.feedType != GunScript.FeedType.Direct || dragAmmo.ammoType != targetGun.ammoType || dragAmmo.itemType != AmmoScript.AmmoItemType.Magazine)
                        {
                            return false;
                        }

                        int roundsToLoad = Mathf.Min(dragAmmo.rounds, targetGun.magCapacity - targetGun.roundsInMag);
                        if (roundsToLoad <= 0)
                        {
                            return true;
                        }

                        dragAmmo.rounds -= roundsToLoad;
                        if (targetGun.racked && targetGun.roundInChamber == GunScript.RoundInChamber.None)
                        {
                            targetGun.roundInChamber = GunScript.RoundInChamber.Round;
                            roundsToLoad--;
                        }

                        targetGun.roundsInMag += roundsToLoad;
                        Sound.Play("gunloadshell", targetGun.transform.position, false, true, null, 1f, 1f, false, false);
                        if (networkAware)
                        {
                            MpCompat.MarkItemChanged(dragAmmo.GetComponent<Item>(), true);
                            MpCompat.MarkItemChanged(targetGun.GetComponent<Item>(), true);
                        }
                        return true;
                    }
                }

                return false;
            }
        }

        public static bool ExecuteQuickLoadMagazine(Body body, AmmoScript magazine, AmmoScript ammo, bool networkAware)
        {
            if (body == null || magazine == null || ammo == null || !IsQuickTransferEnabled || !IsQuickLoadMagazineEnabled)
            {
                return false;
            }

            if (ammo.itemType != AmmoScript.AmmoItemType.Round || magazine.itemType != AmmoScript.AmmoItemType.Magazine || ammo.ammoType != magazine.ammoType || magazine.rounds >= magazine.maxRounds)
            {
                return false;
            }

            List<AmmoScript> ammoList = new List<AmmoScript> { ammo };
            Item sourceItem = ammo.GetComponent<Item>();
            if (sourceItem == null)
            {
                return false;
            }

            Container sourceContainer = sourceItem.ParentContainer();
            if (sourceContainer == null)
            {
                return false;
            }

            foreach (Transform child in sourceContainer.transform)
            {
                AmmoScript childAmmo = child.GetComponent<AmmoScript>();
                if (childAmmo != null && childAmmo != ammo && childAmmo.itemType == AmmoScript.AmmoItemType.Round && childAmmo.ammoType == ammo.ammoType)
                {
                    ammoList.Add(childAmmo);
                }
            }

            using (MpCompat.BeginInventoryMutation(body))
            {
                foreach (AmmoScript ammoScript in ammoList)
                {
                    if (magazine.rounds >= magazine.maxRounds)
                    {
                        break;
                    }

                    magazine.rounds++;
                    if (networkAware)
                    {
                        MpCompat.MarkItemChanged(ammoScript.GetComponent<Item>(), true);
                    }

                    Object.Destroy(ammoScript.gameObject);
                }

                Sound.Play("gunloadshell", magazine.transform.position, false, true, null, 1f, 1f, false, false);
                if (networkAware)
                {
                    MpCompat.MarkItemChanged(magazine.GetComponent<Item>(), true);
                }
            }

            return true;
        }

        [HarmonyPatch(typeof(PlayerCamera), "TryPerformInventoryAction")]
        [HarmonyPrefix]
        private static bool TryPerformInventoryAction_Prefix(PlayerCamera __instance, RaycastResult hit, List<RaycastResult> uiCasts, ref bool __result)
        {
            if (!IsQuickTransferEnabled || !ShouldHandleQuickTransfer())
            {
                return true;
            }

            InvButton invButton = hit.gameObject.GetComponent<InvButton>();
            if (!TryGetValidTransferContext(__instance, invButton, uiCasts, out TransferType transferType, out Item dragItem, out Item targetItem, out Container sourceContainer, out Container targetContainer, out WaterContainerItem sourceWaterContainer, out WaterContainerItem targetWaterContainer, out AmmoScript dragAmmo, out AmmoScript targetAmmo, out GunScript targetGun))
            {
                return true;
            }

            __result = true;

            if (MpCompat.TryHandleQuickTransfer(__instance, transferType, dragItem, targetItem))
            {
                return false;
            }

            if (!MpCompat.IsActive)
            {
                ExecuteQuickTransfer(__instance.body, transferType, dragItem, targetItem, false);
            }

            return false;
        }

        [HarmonyPatch(typeof(AmmoScript), "LoadRound")]
        [HarmonyPrefix]
        private static bool LoadRound_Prefix(AmmoScript __instance, AmmoScript ammo)
        {
            if (!IsQuickTransferEnabled || !IsQuickLoadMagazineEnabled || !Input.GetKey(quickTransferKey.Value))
            {
                return true;
            }

            if (ammo.itemType == AmmoScript.AmmoItemType.Round && __instance.itemType == AmmoScript.AmmoItemType.Magazine && ammo.ammoType == __instance.ammoType && __instance.rounds < __instance.maxRounds)
            {
                if (MpCompat.TryHandleQuickLoadMagazine(__instance, ammo))
                {
                    return false;
                }

                if (!MpCompat.IsActive)
                {
                    ExecuteQuickLoadMagazine(PlayerCamera.main != null ? PlayerCamera.main.body : null, __instance, ammo, false);
                }

                return false;
            }

            return true;
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
            if (IsQuickTransferMagazinesEnabled && dragAmmo != null && targetAmmo != null && dragAmmo.itemType == AmmoScript.AmmoItemType.Magazine && targetAmmo.itemType == AmmoScript.AmmoItemType.Magazine && dragAmmo.ammoType == targetAmmo.ammoType)
            {
                transferType = TransferType.Magazine;
            }

            targetContainer = targetItem.GetComponent<Container>();
            targetWaterContainer = targetItem.GetComponent<WaterContainerItem>();
            sourceContainer = dragItem.ParentContainer();
            sourceWaterContainer = dragItem.GetComponent<WaterContainerItem>();
            if (IsQuickTransferItemsEnabled && targetContainer != null && sourceContainer != null && targetContainer.CanHoldItem(dragItem))
            {
                transferType = TransferType.Item;
            }
            if (IsQuickTransferLiquidsEnabled && targetWaterContainer != null && sourceWaterContainer != null)
            {
                transferType = TransferType.Liquid;
            }

            targetGun = targetItem.GetComponent<GunScript>();
            if (IsQuickTransferDirectFeedRoundsEnabled && targetGun != null && dragAmmo != null && sourceContainer != null && dragAmmo.ammoType == targetGun.ammoType &&
                dragAmmo.itemType == AmmoScript.AmmoItemType.Round)
            {
                transferType = TransferType.DirectFeedRound;
            }
            if (IsQuickTransferDirectFeedMagazinesEnabled && targetGun != null && dragAmmo != null && dragAmmo.ammoType == targetGun.ammoType &&
                dragAmmo.itemType == AmmoScript.AmmoItemType.Magazine)
            {
                transferType = TransferType.DirectFeedMagazine;
            }

            return transferType != TransferType.None;
        }

        private static bool IsTransferTypeEnabled(TransferType transferType)
        {
            switch (transferType)
            {
                case TransferType.Item:
                    return IsQuickTransferItemsEnabled;
                case TransferType.Liquid:
                    return IsQuickTransferLiquidsEnabled;
                case TransferType.Magazine:
                    return IsQuickTransferMagazinesEnabled;
                case TransferType.DirectFeedRound:
                    return IsQuickTransferDirectFeedRoundsEnabled;
                case TransferType.DirectFeedMagazine:
                    return IsQuickTransferDirectFeedMagazinesEnabled;
                default:
                    return false;
            }
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
    }
}
