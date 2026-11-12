namespace WebRPG
{
    // 承載數值條的標題、顯示文字與填滿比例。
    public sealed class AdventureGaugeScreenModel
    {
        public string Title { get; set; }
        public string Text { get; set; }
        public float FillAmount { get; set; }
        public bool CanUse { get; set; }
        public string IconId { get; set; }
        public string Description { get; set; }
        public string CompactText { get; set; }
        public string DisplayName { get; set; }
    }
}
