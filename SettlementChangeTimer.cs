using TaleWorlds.CampaignSystem.Settlements;
using TaleWorlds.ObjectSystem;

namespace ChangeSettlementCulture
{
    public class SettlementChangeTimer
    {
        public uint SettlementId { get; set; }
        public string SettlementName { get; set; }
        public int DaysSinceOwnerChanged { get; set; } = 0;

        public SettlementChangeTimer(uint settlementId, string settlementName)
        {
            SettlementId = settlementId;
            SettlementName = settlementName;
        }
    }
}
