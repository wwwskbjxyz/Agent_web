#pragma once

/* 描述: 卡密信息 */
struct tagCardInfo {
	char*	szWhom;//		制卡人;
	char*	szType;//		卡密类型;
	char*	szIPAddress;//	IP地址;
	char*	szRemarks;//	卡密备注;
	__int64	nCreateTimeStamp;//	卡密创建时间戳;
	__int64	nActivatedTimeStamp;//卡密激活时间戳;
	__int64	nExpiredTimeStamp;//	卡密过期时间戳;
	__int64 nLastLoginTimeStamp;//	卡密最后登录时间戳;
	__int64	nFYI;//				点数;
	int		nOpenMaxNum;//		多开数量;
	int		bBind;//				是否绑定机器(0/1);
	__int64	nBindTime;//			绑机时限(秒);
	__int64	nUnBindDeductTime;//	解绑扣除(秒);
	int		nUnBindMaxNum;//		最多解绑次数;
	int		nUnBindCountTotal;//		累计解绑的总次数;
	__int64 nUnBindDeductTimeTotal;//	累计解绑的总扣除(秒);
	char*	szPCSign;//			机器码
	char*	szSoftwareOwn;//	卡密所属软件位
	int		nUnBindCount;//					周期内 已经解绑的次数;
	int		nFreeUnBindCount;//		属性;	周期内 免费解绑的次数;
	int		Reserved[4];//		保留字段;
};

/* 描述: 设置卡密点数 */
typedef bool(__stdcall* pfn_SP_Cloud_SetCardFYI_v2)(char* szSoftwareName, char* szCard, __int64 nNewFYI);

/* 描述: 设置卡密点数 */
typedef bool(__stdcall* pfn_SP_Cloud_AddCardFYI_v2)(char* szSoftwareName, char* szCard, __int64 nFYI);

/* 描述: 启用卡密 */
typedef bool(__stdcall* pfn_SP_Cloud_EnableCard_v2)(char* szSoftwareName, char* szCard);

/* 描述: 禁用卡密 */
typedef bool(__stdcall* pfn_SP_Cloud_DisableCard_v2)(char* szSoftwareName, char* szCard, char* szCause);

/* 描述: 设置卡密备注 */
typedef bool(__stdcall* pfn_SP_Cloud_SetCardRemarks_v2)(char* szSoftwareName, char* szCard, char* szRemarks);

/* 描述: 设置卡密到期时间 */
typedef bool(__stdcall* pfn_SP_Cloud_SetExpiredTime_v2)(char* szSoftwareName, char* szCard, __int64 ExpiredTime);

/* 描述: 解封卡密 (归还封禁时长) */
typedef bool(__stdcall* pfn_SP_Cloud_EnableCardGiveback_v2)(char* szSoftwareName, char* szCard);

/* 描述: 增减卡密到期时间 */
typedef bool(__stdcall* pfn_SP_Cloud_AddExpiredTime_v2)(char* szSoftwareName, char* szCard, __int64 ExpiredTime);

/* 描述: 解绑卡密 */
typedef bool(__stdcall* pfn_SP_Cloud_UnBindCard_v2)(char* szSoftwareName, char* szCard);

/* 描述: 代理充值 */
typedef bool(__stdcall* pfn_SP_Cloud_AgentRecharge_v2)(char* szSoftwareName, char* szAgent, double Balance, int iHour);

/* 描述: 作者账号创建卡密 (一次一张) */
typedef bool(__stdcall* pfn_SP_Cloud_CreateCard_v2)(char* szSoftwareName, char* szCardType, int iHour, char* szRemarks, IN OUT char* szCard);

/* 描述: 踢掉在线用户, (对应客户端错误码-15) */
typedef bool(__stdcall* pfn_SP_Cloud_Disconnect_v2)(char* szSoftwareName, char* szCard);

/* 描述: 设置返回数据包最大尺寸 */
typedef void(__stdcall* pfn_SP_Cloud_SetBufferSizeMax_v2)(unsigned int iSize);

/* 描述: 用指定代理账号开卡; 返回假时请参考错误信息 (如果创建失败, 代理不会被扣费, 卡密也不会生成) */
/* 参数: szCard; 必须由调用者申请足够大的缓冲区来存放生成的卡密, 单条卡密长度最大为41(包含结尾\0), 比如需要创建10张卡密, 这个缓冲区尺寸应该为10*41; */
/* 参数: szError; 错误信息 */
typedef bool(__stdcall* pfn_SP_Cloud_CreateCardEx_v2)(char* szSoftwareName, char* szAgent, char* szPassword, char* szCardType, int iHour, int iNum, char* szRemarks, IN OUT char* szCard, IN OUT char szError[128]);

/* 描述: 作者账号创建自定义卡密，根据bool来判断是否生成成功 */
/* 参数: szZDYCard; 多条卡密用换行符分割; */
typedef bool(__stdcall* pfn_SP_Cloud_ZDYCreateCard_v2)(char* szSoftwareName, char* szCardType, int iHour, char* szRemarks, IN char* szZDYCard);

/* 描述: 账号充值 */
typedef bool(__stdcall* pfn_SP_Cloud_UserRecharge_v2)(char* szSoftwareName, char* szUser, char* szCard, IN OUT char szError[128]);

/* 描述: 禁用IP(加入黑名单) */
/* 参数: szCause; 原因 */
typedef bool(__stdcall* pfn_SP_Cloud_DisableIP_v2)(char* szIP, char szCause[256]);

/* 描述: 启用IP(移出黑名单) */
typedef bool(__stdcall* pfn_SP_Cloud_EnableIP_v2)(char* szIP);

/* 描述: 禁用机器码(加入黑名单) */
/* 参数: szCause; 原因 */
typedef bool(__stdcall* pfn_SP_Cloud_DisablePCSign_v2)(char* szSoftwareName, char* szPCSign, char szCause[256]);

/* 描述: 启用机器码(移出黑名单) */
typedef bool(__stdcall* pfn_SP_Cloud_EnablePCSign_v2)(char* szSoftwareName, char* szPCSign);


/* 描述: 功能接口 v2 */
struct tagInterface_v2 {
	pfn_SP_Cloud_SetBufferSizeMax_v2	SP_Cloud_SetBufferSizeMax;
	pfn_SP_Cloud_SetCardFYI_v2			SP_Cloud_SetCardFYI;
	pfn_SP_Cloud_AddCardFYI_v2			SP_Cloud_AddCardFYI;
	pfn_SP_Cloud_EnableCard_v2			SP_Cloud_EnableCard;
	pfn_SP_Cloud_EnableCardGiveback_v2	SP_Cloud_EnableCardGiveback;
	pfn_SP_Cloud_DisableCard_v2			SP_Cloud_DisableCard;
	pfn_SP_Cloud_SetCardRemarks_v2		SP_Cloud_SetCardRemarks;
	pfn_SP_Cloud_SetExpiredTime_v2		SP_Cloud_SetExpiredTime;
	pfn_SP_Cloud_AddExpiredTime_v2		SP_Cloud_AddExpiredTime;
	pfn_SP_Cloud_UnBindCard_v2			SP_Cloud_UnBindCard;
	pfn_SP_Cloud_AgentRecharge_v2		SP_Cloud_AgentRecharge;
	pfn_SP_Cloud_CreateCard_v2			SP_Cloud_CreateCard;
	pfn_SP_Cloud_Disconnect_v2			SP_Cloud_Disconnect;
	pfn_SP_Cloud_CreateCardEx_v2		SP_Cloud_CreateCardEx;
	pfn_SP_Cloud_ZDYCreateCard_v2		SP_Cloud_ZDYCreateCard;
	pfn_SP_Cloud_UserRecharge_v2		SP_Cloud_UserRecharge_v2;
	pfn_SP_Cloud_DisableIP_v2			SP_Cloud_DisableIP_v2;
	pfn_SP_Cloud_EnableIP_v2			SP_Cloud_EnableIP_v2;
	pfn_SP_Cloud_DisablePCSign_v2		SP_Cloud_DisablePCSign_v2;
	pfn_SP_Cloud_EnablePCSign_v2		SP_Cloud_EnablePCSign_v2;
	PVOID Reserve[18];
};
