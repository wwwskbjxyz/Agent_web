using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using SProtectAgentWeb.Api.Models;

namespace SProtectAgentWeb.Api.Dtos
{
    /// <summary>获取卡密列表的请求参数。</summary>
    public class CardListRequest
    {
        [Required] public string Software { get; set; } = string.Empty;
        public string Agent { get; set; } = "0";
        public int Page { get; set; } = 1;
        public int Limit { get; set; } = 20;
        public string Status { get; set; } = string.Empty;
        /// <summary>0-模糊，1-精准，2-IP，3-卡密类型</summary>
        public int SearchType { get; set; }
        public IList<string> Keywords { get; set; } = new List<string>();
        /// <summary>是否包含下级制卡人的数据。</summary>
        public bool IncludeDescendants { get; set; } = true;
    }

    /// <summary>卡密列表查询参数。</summary>
    public class CardQueryParams
    {
        public string Software { get; set; } = string.Empty;
        public string? Status { get; set; }
        public int SearchType { get; set; }
        public IList<string> Keywords { get; set; } = new List<string>();
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 20;
        public string CurrentAgent { get; set; } = string.Empty;
        public IList<string> WhomList { get; set; } = new List<string>();
        public bool IncludeDescendants { get; set; } = true;
    }

    /// <summary>卡密列表响应。</summary>
    public class CardListResponse
    {
        public IList<CardInfo> Data { get; set; } = new List<CardInfo>();
        public long Total { get; set; }
    }

    /// <summary>修改卡密状态请求。</summary>
    public class ModifyCardStatusRequest
    {
        [Required] public string Software { get; set; } = string.Empty;
        [Required] public string CardKey { get; set; } = string.Empty;
    }

    /// <summary>生成卡密请求。</summary>
    public class GenerateCardsRequest
    {
        [Required] public string Software { get; set; } = string.Empty;
        [Required] public string CardType { get; set; } = string.Empty;
        [Range(1, 500)] public int Quantity { get; set; } = 1;
        public string Remarks { get; set; } = string.Empty;
        public string? CustomPrefix { get; set; }
    }

    /// <summary>生成卡密响应。</summary>
    public class GenerateCardsResponse
    {
        public int GeneratedCount { get; set; }
        public string CardType { get; set; } = string.Empty;
        public IList<string> SampleCards { get; set; } = new List<string>();
        public IList<string> GeneratedCards { get; set; } = new List<string>();
        public IDictionary<string, object> Cost { get; set; } = new Dictionary<string, object>();
        public string GenerationId { get; set; } = string.Empty;
    }

    /// <summary>激活卡密数量查询请求。</summary>
    public class ActivatedCardCountRequest
    {
        [Required] public string Software { get; set; } = string.Empty;
        public IList<string> CardTypes { get; set; } = new List<string>();
        public string Status { get; set; } = string.Empty;
        /// <summary>支持 yyyy-MM-dd-HH:mm 或 Unix 秒</summary>
        public string? StartTime { get; set; }
        public string? EndTime { get; set; }
        public string? Whom { get; set; }
        public IList<string> WhomList { get; set; } = new List<string>();
        public bool IncludeDescendants { get; set; } = true;
    }

    /// <summary>激活卡密数量查询参数。</summary>
    public class ActivatedCardCountQuery
    {
        public string Software { get; set; } = string.Empty;
        public IList<string> CardTypes { get; set; } = new List<string>();
        public string? Status { get; set; }
        public long? StartTime { get; set; }
        public long? EndTime { get; set; }
        public string CurrentAgent { get; set; } = string.Empty;
        public IList<string> WhomList { get; set; } = new List<string>();
        public bool IncludeDescendants { get; set; } = true;
    }

    /// <summary>激活卡密详情。</summary>
    public class ActivatedCardDetail
    {
        public string Card { get; set; } = string.Empty;
        public long ActivateTime { get; set; }
        public string ActivateTimeText { get; set; } = string.Empty;
    }

    /// <summary>激活卡密统计响应。</summary>
    public class ActivatedCardCountResponse
    {
        public long Count { get; set; }
        public IList<ActivatedCardDetail> Cards { get; set; } = new List<ActivatedCardDetail>();
    }

    /// <summary>最近七天激活趋势请求。</summary>
    public class RecentActivationTrendRequest
    {
        [Required] public string Software { get; set; } = string.Empty;
        public bool IncludeDescendants { get; set; } = true;
        public bool OnlyDescendants { get; set; }
    }

    /// <summary>单日激活统计。</summary>
    public class DailyActivationPoint
    {
        public string Date { get; set; } = string.Empty;
        public long Count { get; set; }
    }

    /// <summary>最近七天激活趋势响应。</summary>
    public class RecentActivationTrendResponse
    {
        public IList<DailyActivationPoint> Points { get; set; } = new List<DailyActivationPoint>();
        public IList<string> Categories { get; set; } = new List<string>();
        public IList<DailyActivationSeries> Series { get; set; } = new List<DailyActivationSeries>();
    }

    /// <summary>代理激活趋势序列。</summary>
    public class DailyActivationSeries
    {
        public string Agent { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public IList<DailyActivationPoint> Points { get; set; } = new List<DailyActivationPoint>();
        public long Total { get; set; }
    }

    /// <summary>卡密使用地区分布请求。</summary>
    public class UsageDistributionRequest
    {
        [Required] public string Software { get; set; } = string.Empty;
        public bool IncludeDescendants { get; set; } = true;
    }

    /// <summary>地区统计。</summary>
    public class LocationStat
    {
        public string Province { get; set; } = string.Empty;
        public string City { get; set; } = string.Empty;
        public string District { get; set; } = string.Empty;
        public long Count { get; set; }
    }

    /// <summary>卡密使用地区分布响应。</summary>
    public class UsageDistributionResponse
    {
        public IList<LocationStat> Provinces { get; set; } = new List<LocationStat>();
        public IList<LocationStat> Cities { get; set; } = new List<LocationStat>();
        public IList<LocationStat> Districts { get; set; } = new List<LocationStat>();
        public long ResolvedTotal { get; set; }
    }
}

