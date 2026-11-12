namespace WebRPG
{
    // 定義地圖列的節點數量與候選節點。
    public sealed class MapRowDefinition
    {
        public int NodeCount { get; set; }
        public NodeDefinition[] NodePool { get; set; }
    }
}
