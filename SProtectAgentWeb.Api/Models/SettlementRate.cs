using System;

namespace SProtectAgentWeb.Api.Models
{
    public sealed class SettlementRate
    {
        private decimal _price;

        /// <summary>
        ///     结算规则所属的代理用户名。
        /// </summary>
        public string AgentUsername { get; set; } = string.Empty;

        /// <summary>
        ///     卡密类型名称。
        /// </summary>
        public string CardType { get; set; } = string.Empty;

        /// <summary>
        ///     结算单价，保留四位小数。
        /// </summary>
        public decimal Price
        {
            get => _price;
            set => _price = Math.Round(value, 4, MidpointRounding.AwayFromZero);
        }
    }
}
