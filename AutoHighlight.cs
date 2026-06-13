using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

namespace ContainerTweaks.Patch
{
    internal partial class ContainerTweaksPatcher
    {
        [HarmonyPatch(typeof(InvButton), "UpdateGraphic")]
        [HarmonyPostfix]
        private static void UpdateGraphic_Postfix(InvButton __instance)
        {
            if (!IsAutoHighlightEnabled) return;

            Item selectedItem = PlayerCamera.main.dragItem;
            Item currentItem = __instance.GetItem();
            if (selectedItem == null || currentItem == null || selectedItem == currentItem)
            {
                return;
            }
            if (IsAutoHighlightWaterContainerEnabled)
            {
                WaterContainerItem selectedWaterContainer = selectedItem.GetComponent<WaterContainerItem>();
                WaterContainerItem currentWaterContainer = currentItem.GetComponent<WaterContainerItem>();
                if (selectedWaterContainer != null && currentWaterContainer != null)
                {
                    TryHighlightWaterContainer(selectedWaterContainer, currentWaterContainer, __instance);
                    return;
                }
            }
            AmmoScript selectedAmmo = selectedItem.GetComponent<AmmoScript>();
            if (selectedAmmo != null)
            {
                if (IsAutoHighlightAmmoEnabled)
                {
                    AmmoScript currentAmmo = currentItem.GetComponent<AmmoScript>();
                    if (currentAmmo != null) { TryHighlightAmmo(selectedAmmo, currentAmmo, __instance); return; }
                }
                if (IsAutoHighlightGunEnabled)
                {
                    GunScript currentGun = currentItem.GetComponent<GunScript>();
                    if (currentGun != null) { TryHighlightGun(selectedAmmo, currentGun, __instance); }
                }
            }
        }

        static void SetButtonColor(InvButton button, Color color)
        {
            Traverse.Create(button).Field<Image>("image").Value.color = color;
        }

        static bool TryHighlightWaterContainer(WaterContainerItem selectedWaterContainer, WaterContainerItem currentWaterContainer, InvButton button)
        {
            if (selectedWaterContainer.CurrentTotal == 0f)
            {
                return false;
            }
            if (CanCombineLiquids(selectedWaterContainer, currentWaterContainer))
            {
                SetButtonColor(button, currentWaterContainer.SpaceLeft > 0f ? HighlightColorCompatible : HighlightColorFull);
                return true;
            }
            List<string> selectedLiquidTypes = GetLiquidTypeList(selectedWaterContainer);
            List<string> currentLiquidTypes = GetLiquidTypeList(currentWaterContainer);
            foreach (var liquidType in selectedLiquidTypes)
            {
                if (!currentLiquidTypes.Contains(liquidType)) return false;
            }
            SetButtonColor(button, HighlightColorPartial);
            return true;
        }

        static bool TryHighlightAmmo(AmmoScript selectedAmmo, AmmoScript currentAmmo, InvButton button)
        {
            if (selectedAmmo.ammoType != currentAmmo.ammoType)
            {
                return false;
            }

            if (selectedAmmo.itemType == AmmoScript.AmmoItemType.Magazine)
            {
                if (selectedAmmo.rounds > 0)
                {
                    if (currentAmmo.itemType == AmmoScript.AmmoItemType.Magazine && currentAmmo.rounds < currentAmmo.maxRounds)
                    {
                        SetButtonColor(button, HighlightColorCompatible);
                        return true;
                    }
                }
                if (selectedAmmo.rounds < selectedAmmo.maxRounds)
                {
                    if (currentAmmo.itemType == AmmoScript.AmmoItemType.Round || (currentAmmo.itemType == AmmoScript.AmmoItemType.Magazine && currentAmmo.rounds > 0))
                    {
                        SetButtonColor(button, HighlightColorFull);
                        return true;
                    }
                }
            }
            else if (selectedAmmo.itemType == AmmoScript.AmmoItemType.Round)
            {
                if (currentAmmo.itemType == AmmoScript.AmmoItemType.Magazine && currentAmmo.rounds < currentAmmo.maxRounds)
                {
                    SetButtonColor(button, HighlightColorCompatible);
                    return true;
                }
            }

            return false;
        }

        static bool TryHighlightGun(AmmoScript selectedAmmo, GunScript currentGun, InvButton button)
        {
            if (currentGun.ammoType != selectedAmmo.ammoType)
                return false;

            if (currentGun.racked && currentGun.roundInChamber == GunScript.RoundInChamber.None)
            {
                SetButtonColor(button, HighlightColorCompatible);
                return true;
            }

            if (currentGun.feedType != GunScript.FeedType.Direct)
                return false;

            if (currentGun.roundsInMag < currentGun.magCapacity)
            {
                SetButtonColor(button, HighlightColorCompatible);
                return true;
            }
            else if (currentGun.roundsInMag >= currentGun.magCapacity)
            {
                SetButtonColor(button, HighlightColorFull);
                return true;
            }

            return false;
        }
    }
}