using System.Globalization;

namespace WebRPGShared
{
    // 驗證並解析冒險操作的兩個整數欄位。
    public static class AdventureCommandPayloadHelper
    {
        public static bool TryRead(string payload, out int index, out int amount)
        {
            index = 0;
            amount = 0;
            string[] fields = (payload ?? string.Empty).Split('|');
            return fields.Length == 2 &&
                int.TryParse(fields[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out index) &&
                int.TryParse(fields[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out amount);
        }
    }
}
