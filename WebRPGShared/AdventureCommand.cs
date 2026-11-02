namespace WebRPGShared
{
    // 定義多人冒險可由伺服器排序的操作識別。
    public static class AdventureCommand
    {
        public const string Vote = "vote";
        public const string Node = "node";
        public const string Resolve = "resolve";
        public const string Reward = "reward";
        public const string Skill = "skill";
        public const string Target = "target";
        public const string Tick = "tick";
        public const string Abandon = "abandon";
    }
}
