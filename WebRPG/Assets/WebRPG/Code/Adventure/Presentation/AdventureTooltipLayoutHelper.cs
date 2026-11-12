using UnityEngine;

namespace WebRPG
{
    // 將提示窗放在游標或鍵盤焦點附近，翻轉方向並內推至畫面範圍。
    internal static class AdventureTooltipLayoutHelper
    {
        private const float Margin = 12;
        private const float PointerOffset = 16;

        public static void PlaceAtPointer(RectTransform root, RectTransform panel, Vector2 screenPosition,
            Camera eventCamera, Vector2 preferredSize)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screenPosition, eventCamera, out Vector2 point))
            {
                return;
            }
            Vector2 size = Fit(root, panel, preferredSize);
            Rect bounds = root.rect;
            float x = point.x + PointerOffset;
            if (x + size.x > bounds.xMax - Margin)
            {
                x = point.x - PointerOffset - size.x;
            }
            float y = point.y - PointerOffset;
            if (y - size.y < bounds.yMin + Margin)
            {
                y = point.y + PointerOffset + size.y;
            }
            ApplyPosition(root, panel, new Vector2(x, y), size);
        }

        public static void Place(RectTransform root, RectTransform panel, RectTransform anchor, Vector2 preferredSize)
        {
            Vector3[] corners = new Vector3[4];
            anchor.GetWorldCorners(corners);
            Vector3 top = root.InverseTransformPoint(corners[1]);
            Vector3 bottom = root.InverseTransformPoint(corners[0]);
            Vector2 size = Fit(root, panel, preferredSize);
            float y = top.y + size.y + Margin <= root.rect.yMax - Margin
                ? top.y + size.y + Margin : bottom.y - Margin;
            ApplyPosition(root, panel, new Vector2(top.x, y), size);
        }

        private static Vector2 Fit(RectTransform root, RectTransform panel, Vector2 preferredSize)
        {
            Vector2 size = new Vector2(Mathf.Min(preferredSize.x, Mathf.Max(1, root.rect.width - 2 * Margin)),
                Mathf.Min(preferredSize.y, Mathf.Max(1, root.rect.height - 2 * Margin)));
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, size.x);
            panel.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, size.y);
            return size;
        }

        private static void ApplyPosition(RectTransform root, RectTransform panel, Vector2 position, Vector2 size)
        {
            Rect bounds = root.rect;
            float x = Mathf.Clamp(position.x, bounds.xMin + Margin, bounds.xMax - size.x - Margin);
            float y = Mathf.Clamp(position.y, bounds.yMin + size.y + Margin, bounds.yMax - Margin);
            panel.position = root.TransformPoint(new Vector3(x, y, 0));
        }
    }
}
