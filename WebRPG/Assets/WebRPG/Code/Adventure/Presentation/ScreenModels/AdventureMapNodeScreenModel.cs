namespace WebRPG
{
    // 承載地圖節點的標題、狀態與選取資訊。
    public sealed class AdventureMapNodeScreenModel
    {
        public string Title { get; set; }
        public string StateText { get; set; }
        public bool CanSelect { get; set; }
        public bool IsChosen { get; set; }
        public int ChoiceIndex { get; set; }
    }
}
