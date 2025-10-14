using System.Collections.Generic;

namespace SProtectAgentWeb.Api.Models;

public class CardInfo
{
    public string Prefix_Name { get; set; } = string.Empty;
    public string? Whom { get; set; }
    public string? CardType { get; set; }
    public int FYI { get; set; }
    public string? State { get; set; }
    public int Bind { get; set; }
    public int OpenNum { get; set; }
    public int LoginCount { get; set; }
    public string? IP { get; set; }
    public string? Remarks { get; set; }
    public long CreateData_ { get; set; }
    public long ActivateTime_ { get; set; }
    public long ExpiredTime_ { get; set; }
    public long LastLoginTime_ { get; set; }
    public int Delstate { get; set; }
    public double Price { get; set; }
    public bool Cty { get; set; }
    public long ExpiredTime__ { get; set; }
    public int UnBindCount { get; set; }
    public int UnBindDeduct { get; set; }
    public int Attr_UnBindLimitTime { get; set; }
    public int Attr_UnBindDeductTime { get; set; }
    public int Attr_UnBindFreeCount { get; set; }
    public int Attr_UnBindMaxCount { get; set; }
    public int BindIP { get; set; }
    public int BanTime { get; set; }
    public string? Owner { get; set; }
    public int BindUser { get; set; }
    public int NowBindMachineNum { get; set; }
    public int BindMachineNum { get; set; }
    public string? PCSign2 { get; set; }
    public int BanDurationTime { get; set; }
    public int GiveBackBanTime { get; set; }
    public int PICXCount { get; set; }
    public int LockBindPcsign { get; set; }
    public long LastRechargeTime { get; set; }
    public byte[]? UserExtraData { get; set; }

    /// <summary>
    /// 与该卡密关联的机器码列表。
    /// </summary>
    public List<string> MachineCodes { get; set; } = new();

    /// <summary>
    /// 兼容只需要单个机器码显示的场景，返回列表中的第一项。
    /// </summary>
    public string? MachineCode => MachineCodes.Count > 0 ? MachineCodes[0] : null;
}
