namespace SProtectAgentWeb.Api.Models;

public class CardType
{
    public string Name { get; set; } = string.Empty;
    public string? Prefix { get; set; }
    public int Duration { get; set; }
    public int FYI { get; set; }
    public double Price { get; set; }
    public string? Param { get; set; }
    public int Bind { get; set; }
    public int OpenNum { get; set; }
    public string? Remarks { get; set; }
    public bool CannotBeChanged { get; set; }
    public int Attr_UnBindLimitTime { get; set; }
    public int Attr_UnBindDeductTime { get; set; }
    public int Attr_UnBindFreeCount { get; set; }
    public int Attr_UnBindMaxCount { get; set; }
    public int BindIP { get; set; }
    public int BindMachineNum { get; set; } = 1;
    public int LockBindPcsign { get; set; }
    public long ActivateTime_ { get; set; }
    public long ExpiredTime_ { get; set; }
    public long LastLoginTime_ { get; set; }
    public int Delstate { get; set; }
    public bool Cty { get; set; }
    public long ExpiredTime__ { get; set; }
}

