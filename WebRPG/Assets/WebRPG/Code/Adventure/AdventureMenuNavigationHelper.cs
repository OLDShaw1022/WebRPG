using System.Linq;

namespace WebRPG
{
    // 將鍵盤導覽限制在目前選單可操作的按鈕內。
    internal static class AdventureMenuNavigationHelper
    {
        public static void Link(params UnityEngine.UI.Button[] buttons)
        {
            UnityEngine.UI.Button[] active = buttons.Where(button => button.gameObject.activeInHierarchy && button.IsInteractable()).ToArray();
            for (int index = 0; index < active.Length; index++)
            {
                UnityEngine.UI.Navigation navigation = new UnityEngine.UI.Navigation
                {
                    mode = UnityEngine.UI.Navigation.Mode.Explicit,
                    selectOnUp = active[(index + active.Length - 1) % active.Length],
                    selectOnDown = active[(index + 1) % active.Length],
                    selectOnLeft = active[index],
                    selectOnRight = active[index]
                };
                active[index].navigation = navigation;
            }
        }
    }
}
