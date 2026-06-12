using HarmonyLib;
using UnityEngine;

namespace ContainerTweaks.Patch
{
    internal partial class ContainerTweaksPatcher
    {
        [HarmonyPatch(typeof(PlayerCamera), "Update")]
        [HarmonyPostfix]
        private static void Update_Postfix(PlayerCamera __instance)
        {
            if (!IsContainerScrollingEnabled || !__instance.containerMenu.activeSelf || __instance.currentContainer == null)
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

            ClampScrollToContainer(__instance);

            if (Mathf.Abs(currentScrollY - targetScrollY) > 0.01f)
            {
                currentScrollY = Mathf.Lerp(currentScrollY, targetScrollY, Time.unscaledDeltaTime * ScrollLerpSpeed);
                ApplyScroll(__instance);
            }
        }

        private static void ResetScroll(PlayerCamera camera)
        {
            currentScrollY = 0f;
            targetScrollY = 0f;
            ApplyScroll(camera);
        }

        private static void ClampScrollToContainer(PlayerCamera camera)
        {
            if (camera == null || camera.currentContainer == null)
            {
                targetScrollY = 0f;
                return;
            }

            int rowCount = Mathf.CeilToInt((float)camera.currentContainer.itemCount / (float)ColumnCount);
            float maxScroll = Mathf.Max(0f, (float)(rowCount - VisibleRowCount) * CellSpacing);
            targetScrollY = Mathf.Clamp(targetScrollY, 0f, maxScroll);
        }

        private static void ApplyScroll(PlayerCamera camera)
        {
            if (!positionsCaptured || camera == null || camera.contPopulate == null)
            {
                return;
            }

            camera.contPopulate.GetComponent<RectTransform>().anchoredPosition = populateOrigPos + new Vector2(0f, Mathf.Round(currentScrollY));
        }
    }
}
