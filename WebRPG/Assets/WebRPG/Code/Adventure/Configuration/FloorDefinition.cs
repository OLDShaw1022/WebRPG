namespace WebRPG
{
    // 定義樓層名稱與地圖列設定。
    public sealed class FloorDefinition
    {
        public string Name { get; set; }
        public MapRowDefinition[] Rows { get; set; }
    }
}
