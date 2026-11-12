namespace WebRPG
{
    // 承載地圖列的狀態文字與節點畫面資料。
    public sealed class AdventureMapRowScreenModel
    {
        public string Status { get; set; }
        public AdventureMapNodeScreenModel[] Nodes { get; set; }
    }
}
