using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace ContainerTweaks.Patch
{
    internal partial class ContainerTweaksPatcher
    {
        [HarmonyPatch(typeof(PlayerCamera), "OpenContainer")]
        [HarmonyPostfix]
        private static void OpenContainer_Postfix(PlayerCamera __instance)
        {
            CaptureOriginalPositions(__instance);

            ResetScroll(__instance);
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

            if (IsContainerViewExpansionEnabled)
            {
                ReflowContainerGrid(__instance);
            }

            if (IsContainerScrollingEnabled)
            {
                ClampScrollToContainer(__instance);
                ApplyScroll(__instance);
            }
        }

        private static void CaptureOriginalPositions(PlayerCamera camera)
        {
            if (positionsCaptured || camera == null || camera.contPopulate == null)
            {
                return;
            }

            populateOrigPos = camera.contPopulate.GetComponent<RectTransform>().anchoredPosition;
            positionsCaptured = true;
        }

        private static void ReflowContainerGrid(PlayerCamera camera)
        {
            if (!positionsCaptured || camera.containerUnloadButtons == null)
            {
                return;
            }

            for (int i = 0; i < camera.containerUnloadButtons.Count; i++)
            {
                GameObject gameObject = camera.containerUnloadButtons[i];
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
