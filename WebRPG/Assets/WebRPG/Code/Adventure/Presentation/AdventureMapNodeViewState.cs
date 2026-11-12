namespace WebRPG
{
    // 保存地圖節點控制項及其選取與顯示狀態。
    internal sealed class AdventureMapNodeViewState
    {
        public AdventureMapNodeViewState(AdventureButtonViewReferences control)
        {
            Control = control;
        }

        public AdventureButtonViewReferences Control { get; }
        public int ChoiceIndex { get; set; }
        public bool HasState { get; set; }
        public bool CanSelect { get; set; }
        public bool IsChosen { get; set; }
    }
}
