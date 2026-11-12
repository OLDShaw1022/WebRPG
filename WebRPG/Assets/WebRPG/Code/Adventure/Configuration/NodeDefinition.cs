namespace WebRPG
{
    // 定義地圖節點的類型、遭遇與獎勵或代價。
    public sealed class NodeDefinition
    {
        public string Name { get; set; }
        public AdventureNodeType Type { get; set; }
        public string[] EnemyGroupPool { get; set; }
        public int Gold { get; set; }
        public int Healing { get; set; }
        public int HealthCost { get; set; }
        public bool IsBattle => Type == AdventureNodeType.Battle || Type == AdventureNodeType.Elite
            || Type == AdventureNodeType.Boss;
    }
}
