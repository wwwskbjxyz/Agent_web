#ifndef _SP_VERIFY_HEADER
#define _SP_VERIFY_HEADER

#include <Windows.h>
#include <vector>
#include <memory>
#include "SPVerifyDefine.h"

//链接lib
#if _WIN64
#include "SPPicShieldx64.h"
	#if _MT
		#if _DEBUG
			#if _DLL
				#pragma comment(lib,"SPVerifyx64_MDd.lib")//MDd
			#else
				#pragma comment(lib,"SPVerifyx64_MTd.lib")//MTd
			#endif // _DLL
		#else
			#if _DLL
				#pragma comment(lib,"SPVerifyx64_MD.lib")//MD
			#else
				#pragma comment(lib,"SPVerifyx64_MT.lib")//MT
			#endif // _DLL
		#endif // _DEBUG
	#endif // _MT
#else
#include "SPPicShieldx32.h"
	#if _MT
		#if _DEBUG
			#if _DLL
				#pragma comment(lib,"SPVerifyx32_MDd.lib")//MDd
			#else
				#pragma comment(lib,"SPVerifyx32_MTd.lib")//MTd
			#endif // _DLL
		#else
			#if _DLL
				#pragma comment(lib,"SPVerifyx32_MD.lib")//MD
			#else
				#pragma comment(lib,"SPVerifyx32_MT.lib")//MT
			#endif // _DLL
		#endif // _DEBUG
	#endif // _MT
#endif // _WIN64






//
// 错误代码
//

enum enum_SPErrorCode
{
	SP_NOERROR = 0,			/* 正常 */
	SP_NOINIT = -1,			/* 没有正常初始化 */
	SP_WSAFAILED = -2,		/* WSAStartup函数返回失败 */
	SP_CONNECTFAILED = -3, /* 尝试3次后, 网络通讯仍然出错 */
	SP_DATAERROR = -4,		/* 尝试3次后, 数据包仍然异常 */
	SP_NOLOADCLOUDDLL = -5, /* 未加载云计算插件或云计算插件未导出SP_CloudComputing_Callback函数 */
	SP_FAILEDWRITE = -6,	/* 提供的指针不可写 */
	SP_EXPIREDTOKEN = -15,	/* 身份认证信息已过期!请重新登录 */
	SP_UNKNOWNERRROR = -16,	/* 未知错误 */
	SP_INVALIDCARD = -21,	/* 卡密/账号密码无效或错误 */
	SP_EXPIREDCARD = -22,	/* 卡密/账号已过期 */
	SP_BANNEDCARD = -23,	/* 卡密/账号被封停 */
	SP_INVALIDAGENT = -24,	/* 代理商无效 */
	SP_BANNEDAGENT = -25,	/* 代理商被停用 */
	SP_MAXONLINE = -26,		/* 已达到最大在线数量 */
	SP_OFFLINE = -27,		/* 当前客户端身份认证信息已被移除（可能是被服务器主动踢下线或被其他连接登录挤下线） */
	SP_ANOTHERUSERUNBIND = -28, /* 已在其他机器上解绑，当前连接已下线 */
	SP_RESERVE1 = -29, /* 保留，暂时不用 */
	SP_QUERYAGENTEXCEPTION = -30, /* 代理商异常 */
	SP_INVALIDPARAM = -31, /* 传输参数错误（格式，或超过限度） */
	SP_FYINOTENOUGH = -32, /* 当前卡密剩余点数不足(扣除点数＞剩余点数) */
	SP_RESERVE2 = -33, /* 保留，暂时不用 */
	SP_NOACTIVATEDCARD = -38,/* 卡密还未激活 */
	SP_LOGINEXCEPTION = -39,/* 出现异常状况，无法登录 */
	SP_FAILEDACTIVATECARD = -40,/* 无法激活卡密 */
	SP_BINDMSGDIFF = -41,	/* 当前设备信息与卡号绑定信息不一致 */
	SP_FAILEDGETCARD = -42,	/* 读取卡密属性时出错 */
	SP_NOBIND = -43,		/* 卡密/账号没有设置绑定信息或当前机器与卡密/账号的绑定信息一致 */
	SP_MAXUNBINDCOUNT = -44,/* 已达到最大解绑次数 */
	SP_NOENOUGHTIME = -45,	/* 剩余时间不足以解绑 */
	SP_FAILEDUNBIND = -46,	/* 解绑失败 */
	SP_UNBINDEXCEPTION = -47, /* 尝试解绑时发生错误 */
	SP_INVALIDUSERNAME = -48, /* 账号名被占用或无效 */
	SP_INVALIDPWD = -49,	 /* 密码无效 */
	SP_INVALIDRECHARGECARD = -50, /* 充值卡无效 */
	SP_INVALIDSPWD = -51,	/* 超级密码无效或错误 */
	SP_OPERATEEXCEPTION = -52,  /* 操作时出现错误 */
	SP_TRIALMSGDIFF = -53, /* 试用卡绑定信息与登录机器不一致 */
	SP_APPLIEDTRIAL = -54, /* 已经申请过试用 */
	SP_TRIALERROR = -55,   /* 申请试用时出错 */
	SP_EXPIREDTRIAL = -56, /* 试用已到期 */
	SP_UNBINDTRIAL = -57, /* 试用卡不可解绑 */
	SP_CLOSEFAILED = -58, /* 关闭客户端失败 */
	SP_DISABLETRIAL = -60,   /* 禁止试用 */
	SP_DISABLELOGIN = -61,	/* 禁止登录 */
	SP_DISABLEREGISTER = -62,	/* 禁止注册 */
	SP_DISABLERECHARGE = -63,	/* 禁止充值 */
	SP_DISABLEMIXCARDRECHARGE = -64,	/* 禁止充值不同卡类型的卡密 */
	SP_DISABLEMIXAGENTRECHARGE = -65,	/* 禁止充值不同代理的卡密 */
	SP_NOMATCHPCSIGN = -66, /* 服务端只允许解绑当前登录设备相同的机器码 */
	SP_NOEXISTPCSIGN = -67, /* 欲解绑的机器码不存在 */
	SP_TRIALCARDUSEDFORRECHARGING = -68, /* 无法将试用卡用于充值 */
	SP_ACTIVATEDCARDUSEDFORRECHARGING = -69, /* 无法将已激活的卡用于充值 */
	SP_USEDCARD = -70, /* 充值卡已被使用 */
	SP_TRIALCARDMUSTREGISTEREDSEPARATELY = -71, /* 试用卡无法跟普通卡混合注册 */
};


//
// 解绑信息
//

struct st_SPUnBindInfo
{
	int RemainUnBindCount; //剩余解绑次数
	int RemainFreeCount; //剩余免费解绑次数
	__int64 UnBindLimitTime; //解绑时限 单位:秒
	__int64 UnBindDeductTime; //解绑扣时,免费解绑时该值为0 单位:秒
};


//
// 更新信息
//

struct st_SPUpdateVersionInfo
{
	int	UpdateVer;		// 最新版本号
	BOOL UpdateForce;	// 是否强制更新
	BOOL UpdateDirectUrl;// 是否直链下载
	char UpdateUrl[2049];	// 更新下载地址
	char UpdateRunExe[101];	// 更新后需运行的exe
	char UpdateRunCmd[129];	// 更新后需运行的exe的附加参数
};

//
// 充值信息
//

struct st_SPUserRechargeInfo
{
	__int64	nOldExpiredTimeStamp;//	旧的卡密过期时间戳;
	__int64	nNewExpiredTimeStamp;//	新的卡密过期时间戳;
	__int64	nOldCount;			// 旧的充值卡张数;
	__int64	nNewCount;			// 新的充值卡张数;
	int		nRechargeCount;		// 充值的卡密个数;
};

//
// 配置信息
//
struct st_SPServerOption {
	BOOL DisableTrial;		// TRUE为禁止试用
	BOOL DisableLogin;		// TRUE为禁止软件登录
	BOOL DisableRegister;	// TRUE为禁止软件注册
	BOOL DisableRecharge;	// TRUE为禁止软件充值
	BOOL DisableGetCountInfo;	// TRUE为禁止软件使用 [获取频率验证总在线数 SP_Cloud_GetOnlineTotalCount]和[获取在线卡密数 SP_Cloud_GetOnlineCardsCount]
	int		Reserved[15];//			保留字段;
};

//
// 客户端在线信息
//
struct st_SPOnlineInfo {
	int		nCID;//客户端ID
	const char* szComputerName;//客户端的电脑名
	const char* szWinVer;//客户端的windows版本
	__int64		LoginTS;//客户端的登录时间
};

//
// 客户端在线信息头部
//
struct st_SPOnlineInfoHead {
	int		nCount;//在线客户端个数
	const st_SPOnlineInfo* Info;//客户端在线信息指针，可以根据个数遍历
};

/* 描述: 客户端已绑定机器信息 */
struct st_SPPCSignInfo {
	__int64	BindTS;//绑定时间戳
	const char* szPCSign;//机器码
	const char* szComputerName;//机器的电脑名
	const char* szWinVer;//机器的windows版本
	const char* szRemark;//机器的备注 保留
	__int64	LastLoginTS;//机器的最后登录时间戳
};

/* 描述: 客户端已绑定机器信息头 */
struct st_SPPCSignInfosHead {
	int		nCount;//绑定机器的个数
	const st_SPPCSignInfo* Info;//已绑定机器信息指针
};

/* 描述: 客户端已绑定机器参数 */
struct st_SPQueryPCSignInfos {
	BOOL bBindIP; // 是否绑定了IP
	BOOL bLockBindPCSign; // 是否仅允许移除自身的机器码 若为TRUE则无法移除自身以外的其他已绑定机器码
	uint32_t	RestCount;// 周期内剩余次数
	__int64		RefreshUnbindRemainSeconds;// 秒数; `距离[换绑次数更新时间]还剩：xxx天xx时xx分
	st_SPPCSignInfosHead* PCSignInfosHead; // 客户端已绑定机器信息头
};

//
// 导出
//

extern "C" {

	/* 描述: 验证初始化 */
	/* 参数: szConnectKey; 连接密钥 打开服务端的独立软件管理界面 双击对应的软件位后可以找到 */
	/* 参数: iTimeout; send/recv超时时间(毫秒) */
	/* 参数: bIgnoreHint; 忽略加壳提示，默认为false即不忽略 */
	/* 返回: 参考云计算iError  */
	int  __stdcall __SP_Verify_Init(const char* szConnectKey, int iTimeout, bool bIgnoreHint = false);

	/* 描述: 启用云计算PIC (不联网) */
	/* 云计算内使用PIC效率太低,所以默认禁用 */
	/* 调用该函数后,可在云计算类函数返回成功后使用相应的PIC end,如果为非法/破解调用则会崩溃 */
	/* 该函数调用一次即可 */
	/* 返回: 无  */
	void __stdcall __SP_Cloud_PICEnable();

	/* 描述: 获取公告 (每次调用联网)*/
	/* 参数: pNotice; 接收公告 */
	/* 返回: iError; 云计算错误码/状态码 */
	int  __stdcall __SP_Verify_GetNotice(OUT char* pNotice);

	/* 描述: 验证 获取最新版本信息 (每次调用联网) */
	/* 参数: VersionInfo; 最新版本信息 */
	/* 返回: iError; 云计算错误码/状态码  */
	int  __stdcall __SP_Verify_GetLastestVersionInfo(OUT st_SPUpdateVersionInfo* VersionInfo);

	/* 描述: 验证, 获取服务端配置信息 (每次调用联网)*/
	/* 参数: pServerOption; 接收配置信息 */
	/* 返回: iError; 云计算错误码/状态码 */
	int  __stdcall __SP_Verify_GetServerOption(OUT st_SPServerOption* pServerOption);

	/* 描述: 验证, 拉黑当前机器的机器码 (每次调用联网)*/
	/* 返回: iError; 云计算错误码/状态码 */
	int  __stdcall __SP_Verify_DisablePCSign();

	/* 描述: 验证, 拉黑当前机器的IP (每次调用联网)*/
	/* 返回: iError; 云计算错误码/状态码 */
	int  __stdcall __SP_Verify_DisableIP();

	/* 描述: 验证, 反调试/检测调试器 (不联网)*/
	/* 返回: 是否被调试 */
	bool __stdcall __SP_Verify_AntiDebugger();

	/* 描述: 验证设置Socks5代理 默认不开启Socks5代理 由客户端直连服务端 */
	/* 参数: bEnable; 是否启用Socks5代理 如果在启用Socks5代理后需要临时禁用则传入false */
	/* 参数: szIP; Socks5代理IP地址/域名 */
	/* 参数: wPort; Socks5代理端口号 */
	/* 参数: szUsername; Socks5代理账号 没有则传入NULL */
	/* 参数: szPassword; Socks5代理密码 没有则传入NULL */
	void  __stdcall __SP_Verify_SetSocks5(bool bEnable = true, const char* szHost = NULL, int wPort = 0, const char* szUsername = NULL, const char* szPassword = NULL);

	/* 描述: 验证, 设置验证实际访问的IP或域名，可用于多线路，注意，服务端与客户端的链接密钥仍要保持一致,也需要在调用SP_Verify_Init后再使用 */
	/* 参数: szHost; IP或域名 */
	/* 参数: wPort; 端口 */
	void __stdcall __SP_Verify_SetHost(const char* szHost, unsigned short wPort);

	/* 描述: 验证, 获取试用卡,服务器关闭试用则无效 (每次调用联网)*/
	/* 参数: szCard; 输出获取到的试用卡 */
	/* 返回: iError; 云计算错误码/状态码 */
	int  __stdcall __SP_Verify_GetTrialCard(OUT char szCard[41]);

	/* 描述: 验证 卡密登录 */
	/* 参数: szCard; 卡密 */
	/* 返回: 参考云计算iError  */
	int  __stdcall __SP_Verify_CardLogin(const char* szCard);

	/* 描述: 验证 账号密码登录 */
	/* 参数: szUsername; 账号 */
	/* 参数: szPassword; 密码 */
	/* 返回: 参考云计算iError  */
	int  __stdcall __SP_Verify_UserLogin(const char* szUsername, const char* szPassword);

	/* 描述: 验证 卡密解绑 (每次调用联网)*/
	/* 参数: szCard; 卡密 */
	/* 参数: UnBindInfo; 接收解绑返回信息(剩余解绑次数,解绑扣时等) */
	/* 返回: 参考云计算iError */
	int  __stdcall __SP_Verify_CardUnbind(const char* szCard, OUT st_SPUnBindInfo* UnBindInfo);

	/* 描述: 验证 账号解绑 (每次调用联网)*/
	/* 参数: szCard; 卡密 */
	/* 参数: UnBindInfo; 接收解绑返回信息(剩余解绑次数,解绑扣时等) */
	/* 返回: 参考云计算iError */
	int  __stdcall __SP_Verify_UserUnbind(const char* szUsername, const char* szPassword, OUT st_SPUnBindInfo* UnBindInfo);

	/* 描述: 验证 账号充值 */
	/* 参数: szUsername; 账号 */
	/* 参数: szRechargeCards; 充值卡 如需填写多个充值卡请用换行符拼接 */
	/* 参数: RechargeInfo; 接收充值返回信息 */
	/* 返回: 参考云计算iError  */
	int  __stdcall __SP_Verify_UserRecharge(const char* szUsername, const char* szRechargeCards, OUT st_SPUserRechargeInfo* RechargeInfo);

	/* 描述: 验证 账号注册 */
	/* 参数: szUsername; 账号 */
	/* 参数: szPassword; 密码 */
	/* 参数: szSuperPWD; 超级密码 */
	/* 参数: szRechargeCards; 充值卡 如需填写多个充值卡请用换行符拼接 */
	/* 返回: 参考云计算iError  */
	int  __stdcall __SP_Verify_UserRegister(const char* szUsername, const char* szPassword, const char* szSuperPWD, const char* szRechargeCards);

	/* 描述: 验证 账号改密 */
	/* 参数: szUsername; 账号 */
	/* 参数: szSuperPWD; 超级密码 */
	/* 参数: szNewPWD; 新密码 */
	/* 返回: 参考云计算iError  */
	int  __stdcall __SP_Verify_UserChangePWD(const char* szUsername, const char* szSuperPWD, const char* szNewPWD);

	/* 描述: 验证, 获取卡密当前所有在线客户端信息,需卡密有效 (每次调用联网)*/
	/* 返回的客户端信息会保存到库申请的空间 在下次调用CardQueryOnlineClient或UserQueryOnlineClient销毁*/
	/* 参数: szCard; 卡密 */
	/* 参数: pOnlineInfoHead; 在线客户端信息 */
	/* 返回: iError; 云计算错误码/状态码 */
	int  __stdcall __SP_Verify_CardQueryOnlineClient(const char* szCard, OUT st_SPOnlineInfoHead* pOnlineInfoHead);

	/* 描述: 验证, 获取某个账号当前在线客户端信息,需账号密码有效 (每次调用联网)*/
	/* 返回的客户端信息会保存到库申请的空间 在下次调用CardQueryOnlineClient或UserQueryOnlineClient销毁*/
	/* 参数: szUsername; 账号 */
	/* 参数: szPassword; 密码 */
	/* 参数: pOnlineInfoHead; 在线客户端信息 */
	/* 返回: iError; 云计算错误码/状态码 */
	int  __stdcall __SP_Verify_UserQueryOnlineClient(const char* szUsername, const char* szPassword, OUT st_SPOnlineInfoHead* pOnlineInfoHead);

	/* 描述: 验证, 关闭卡密其他在线连接,需卡密有效 (每次调用联网)*/
	/* 参数: szCard; 卡密 */
	/* 参数: iCID; 客户端ID 从SP_Verify_CardQueryOnlineClient函数可获得 */
	/* 返回: iError; 云计算错误码/状态码 */
	int  __stdcall __SP_Verify_CardCloseOnlineByCID(const char* szCard, int iCID);

	/* 描述: 验证, 关闭账号其他在线连接,需账号密码有效 (每次调用联网)*/
	/* 参数: szUsername; 账号 */
	/* 参数: szPassword; 密码 */
	/* 参数: iCID; 客户端ID 从SP_Verify_UserQueryOnlineClient函数可获得 */
	/* 返回: iError; 云计算错误码/状态码 */
	int  __stdcall __SP_Verify_UserCloseOnlineByCID(const char* szUsername, const char* szPassword, int iCID);

	/* 描述: 验证, 获取卡密已绑定的所有的机器码信息,需卡密有效 (每次调用联网)*/
	/* 返回的机器码信息会保存到库申请的空间 在下次调用CardQueryPCSigns或UserQueryPCSigns销毁*/
	/* 参数: szCard; 卡密 */
	/* 参数: pPCSignInfos; 已绑定机器码信息 */
	/* 返回: iError; 云计算错误码/状态码 */
	int  __stdcall __SP_Verify_CardQueryPCSignInfos(const char* szCard, OUT st_SPQueryPCSignInfos* pPCSignInfos);

	/* 描述: 验证, 获取账号已绑定的所有的机器信息,需账号密码有效 (每次调用联网)*/
	/* 返回的机器码信息会保存到库申请的空间 在下次调用CardQueryPCSigns或UserQueryPCSigns销毁*/
	/* 参数: szUsername; 账号 */
	/* 参数: szPassword; 密码 */
	/* 参数: pPCSignInfos; 已绑定机器码信息 */
	/* 返回: iError; 云计算错误码/状态码 */
	int  __stdcall __SP_Verify_UserQueryPCSignInfos(const char* szUsername, const char* szPassword, OUT st_SPQueryPCSignInfos* pPCSignInfos);

	/* 描述: 验证, 移除卡密的某个已绑定机器码,需卡密有效 (每次调用联网)*/
	/* 参数: szCard; 卡密 */
	/* 参数: szUnbindPCSign; 欲移除的机器码 传入NULL或者0字符则表示为本机机器码 */
	/* 参数: bUnbindIP; 是否同时解除IP的绑定 */
	/* 返回: iError; 云计算错误码/状态码 */
	int  __stdcall __SP_Verify_CardRemovePCSign(const char* szCard, const char* szUnbindPCSign, bool bUnbindIP);

	/* 描述: 验证, 关闭账号其他在线连接,需账号密码有效 (每次调用联网)*/
	/* 参数: szUsername; 账号 */
	/* 参数: szPassword; 密码 */
	/* 参数: szUnbindPCSign; 欲移除的机器码 传入NULL或者0字符则表示为本机机器码 */
	/* 参数: bUnbindIP; 是否同时解除IP的绑定 */
	/* 返回: iError; 云计算错误码/状态码 */
	int  __stdcall __SP_Verify_UserRemovePCSign(const char* szUsername, const char* szPassword, const char* szUnbindPCSign, bool bUnbindIP);

	/* 描述: 验证, 通过账号的关联卡找回账号密码 (每次调用联网)*/
	/* 参数: szCard; 充值到账号里的卡密或者账号的主卡 */
	/* 参数: szUsername; 账号的账号名 */
	/* 参数: szPassword; 账号的密码 */
	/* 参数: szSuperPWD; 账号的超级密码 */
	/* 返回: iError; 云计算错误码/状态码 */
	int  __stdcall __SP_Verify_UserRetrievePassword(const char* szCard, OUT char szUsername[33], OUT char szPassword[33], OUT char szSuperPWD[33]);

	/* 描述: 查询是否已登录 原理是调用一次心跳包 (每次调用联网)*/
	/* 返回: 是否登录 */
	bool  __stdcall __SP_Verify_IsLogin();

	/* 描述: 查询错误号的详细信息 (不联网)*/
	/* 参数: iError; 云计算错误码/状态码 */
	/* 参数: pMsg; 接收错误信息 */
	void  __stdcall __SP_Verify_GetErrorMsg(int iError, OUT char* pMsg);

	/* 描述: 云计算请求 (每次调用联网) */
	/* 参数: dwCloudID; 云计算ID; (必须大于0) */
	/* 参数: pData; 云计算数据包指针 */
	/* 参数: dwLength; 云计算数据包长度 */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 云计算是否成功; 如果出错, 可参考iError */
	bool  __stdcall __SP_CloudComputing(int dwCloudID, unsigned char* pData, int dwLength, int* iError);

	/* 描述: 云计算请求 (数据无加密) */
	/* 参数: dwCloudID; 云计算ID */
	/* 参数: pData; 云计算数据包指针 */
	/* 参数: dwLength; 云计算数据包长度 */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 云计算是否成功; 如果出错, 可参考iError */
	bool  __stdcall __SP_CloudComputing_NoEncrypt(int dwCloudID, unsigned char* pData, int dwLength, int* iError);

	/* 描述: 云计算返回数据 */
	/* 参数: dwYunID; 云计算ID; (必须大于0) */
	/* 参数: pData; 存放读取内容的缓冲区 */
	/* 参数: dwLength; 要读取的长度 */
	/* 返回: 读取到的长度 */
	int  __stdcall __SP_CloudResult(int dwCloudID, unsigned char* pData, int dwLength);

	/* 描述: 云计算, 返回数据总长度 */
	/* 参数: dwCloudID; 云计算ID; (必须大于0) */
	/* 返回: 总长度 */
	int  __stdcall __SP_CloudResultLength(int dwCloudID);

	/* 描述: 云计算, 返回数据剩余未读长度 */
	/* 参数: dwCloudID; 云计算ID; (必须大于0) */
	/* 返回: 读取到的长度 */
	int  __stdcall __SP_CloudResultRest(int dwCloudID);

	/* 描述: 云计算, 频率验证 (建议创建一条线程来频繁调用, 比如30秒调用一次) */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否验证成功; 如果出错, 可参考iError  */
	bool  __stdcall __SP_Cloud_Beat(int* iError);

	/* 描述: 云计算, 获取当前登陆卡密所属代理名 (每次调用联网) */
	/* 参数: szAgent[44] */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	bool  __stdcall __SP_Cloud_GetCardAgent(char szAgent[44], int* iError);

	/* 描述: 云计算, 获取当前登陆卡密的卡类型 (每次调用联网) */
	/* 参数: szCardType[36] */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	bool  __stdcall __SP_Cloud_GetCardType(char szCardType[36], int* iError);

	/* 描述: 云计算, 获取当前登陆卡密登录时记录的IP地址 (每次调用联网) */
	/* 参数: szIPAddress[44] */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	bool  __stdcall __SP_Cloud_GetIPAddress(char szIPAddress[44], int* iError);

	/* 描述: 云计算, 获取当前登陆卡密的备注 (每次调用联网) */
	/* 参数: szRemarks[132] */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	bool  __stdcall __SP_Cloud_GetRemarks(char szRemarks[132], int* iError);

	/* 描述: 云计算, 获取当前登陆卡密的创建时间戳 (每次调用联网) */
	/* 参数: iCreatedTimeStamp */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	bool  __stdcall __SP_Cloud_GetCreatedTimeStamp(__int64* iCreatedTimeStamp, int* iError);

	/* 描述: 云计算, 获取当前登陆卡密的激活时间戳 (每次调用联网) */
	/* 参数: iActivatedTimeStamp */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	bool  __stdcall __SP_Cloud_GetActivatedTimeStamp(__int64* iActivatedTimeStamp, int* iError);

	/* 描述: 云计算, 获取当前登陆卡密的过期时间戳 (每次调用联网) */
	/* 参数: iExpiredTimeStamp */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	bool  __stdcall __SP_Cloud_GetExpiredTimeStamp(__int64* iExpiredTimeStamp, int* iError);

	/* 描述: 云计算, 获取当前登陆卡密的最后登录时间戳 (每次调用联网) */
	/* 参数: iLastLoginTimeStamp */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	bool  __stdcall __SP_Cloud_GetLastLoginTimeStamp(__int64* iLastLoginTimeStamp, int* iError);

	/* 描述: 云计算, 获取当前登陆卡密的剩余点数 (每次调用联网) */
	/* 参数: iFYI */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	bool  __stdcall __SP_Cloud_GetFYI(__int64* iFYI, int* iError);

	/* 描述: 云计算, 获取当前登陆卡密的多开数量属性值 (每次调用联网) */
	/* 参数: iNum */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	bool  __stdcall __SP_Cloud_GetOpenMaxNum(int* iNum, int* iError);

	/* 描述: 云计算, 获取当前登陆卡密的绑定机器属性值 (每次调用联网) */
	/* 参数: iBind; 是否绑机, 1/0 */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	bool  __stdcall __SP_Cloud_GetBind(int* iBind, int* iError);

	/* 描述: 云计算, 获取当前登陆卡密的换绑周期 (每次调用联网) */
	/* 参数: iBindTime; (秒) */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	bool  __stdcall __SP_Cloud_GetBindTime(__int64* iBindTime, int* iError);

	/* 描述: 云计算, 获取当前登陆卡密的解绑扣除属性值 (每次调用联网) */
	/* 参数: iDeductSec; (秒) */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	bool  __stdcall __SP_Cloud_GetUnBindDeductTime(__int64* iDeductSec, int* iError);

	/* 描述: 云计算, 获取当前登陆卡密的最多解绑次数属性值 (每次调用联网) */
	/* 参数: iNum */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	bool  __stdcall __SP_Cloud_GetUnBindMaxNum(int* iNum, int* iError);

	/* 描述: 云计算, 获取当前登陆卡密的累计解绑次数 (每次调用联网) */
	/* 参数: iCountTotal */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	bool  __stdcall __SP_Cloud_GetUnBindCountTotal(int* iCountTotal, int* iError);

	/* 描述: 云计算, 获取当前登陆卡密的累计解绑扣除的时间 (每次调用联网) */
	/* 参数: iDeductTimeTotal; (秒) */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	bool  __stdcall __SP_Cloud_GetUnBindDeductTimeTotal(__int64* iDeductTimeTotal, int* iError);

	/* 描述: 云计算, 获取当前登陆卡密周期内的解绑次数 (每次调用联网) */
	/* 参数: iCount */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	bool  __stdcall __SP_Cloud_GetUnBindCount(int* iCount, OUT OPTIONAL int* iError);

	/* 描述: 云计算, 获取频率验证总在线数量 (每次调用联网, 该功能需要在服务端 [独立软件管理] 开启) */
	/* 参数: iTotalCount; 总在线数量 */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	bool  __stdcall __SP_Cloud_GetOnlineTotalCount(unsigned int* iTotalCount, OUT OPTIONAL int* iError);

	/* 描述: 云计算, 获取在线卡密数量 (每次调用联网, 该功能需要在服务端 [独立软件管理] 开启) */
	/* 参数: iOnlineCardsCount; 在线卡密数量 */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	/* 说明: */
	/*		比如有100张多开数量为10的卡密, 所有卡密都已登录占满多开数量 */
	/*		此时服务端拥有100*10=1000条在线链接 */
	/*		1000条在线链接中实际上是有100张在线卡密, 调用当前接口后, iTotalCount值则为100 */
	bool  __stdcall __SP_Cloud_GetOnlineCardsCount(unsigned int* iOnlineCardsCount, OUT OPTIONAL int* iError);

	/* 描述: 云计算, 获取指定卡密在线链接数量 (每次调用联网) */
	/* 参数: szCard; 卡密; 填写NULL为当前登录云计算的卡密 */
	/* 参数: iOnlineCount; 在线数量 */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	/* 说明: */
	/*		比如有1张多开数量为10的卡密 */
	/*		此时用户使用这张卡密登录了3个客户端, 调用当前接口后, iTotalCount值则为3 */
	bool  __stdcall __SP_Cloud_GetOnlineCountByCard(OPTIONAL const char* szCard, unsigned int* iOnlineCount, OUT OPTIONAL int* iError);

	/* 描述: 云计算, 移除当前云计算身份认证信息 (每次调用联网) */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 下线是否成功; 如果出错, 可参考iError  */
	/* 调用该函数后 程序会无法通过PIC校验 请提醒用户重新启动程序 */
	bool  __stdcall __SP_Cloud_Offline(int* iError);

	/* 描述: 云计算, 获取当前登陆的卡密 (登录成功后可用) */
	/* 参数: szCard */
	void  __stdcall __SP_Cloud_GetCard(OUT char szCard[41]);

	/* 描述: 云计算, 获取当前登陆的账号 (登录成功后可用) */
	/* 参数: szUser */
	void  __stdcall __SP_Cloud_GetUser(OUT char szUser[33]);

	/* 描述: 云计算, 禁用当前登陆的卡密 (登录成功后可用) */
	/* 参数: iError; 云计算错误码/状态码 */
	void  __stdcall __SP_Cloud_DisableCard(OUT OPTIONAL int* iError);

	/* 描述: 云计算, 获取当前客户端ID (登录成功后可用) */
	int  __stdcall __SP_Cloud_GetCID();

	/* 描述: 云计算, 获取当前卡密在线客户端数量 (S登录成功后可用) */
	/* 参数: iError; 云计算错误码/状态码 */
	bool  __stdcall __SP_Cloud_GetOnlineCount(int* iCount, int* iError);

	/* 描述: 云计算, 获取当前设备机器码 (每次调用联网) */
	/* 参数: szPCSign 输出机器码 */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	bool  __stdcall __SP_Cloud_GetPCSign(char szPCSign[33], OUT OPTIONAL int* iError);

	/* 描述: 云计算, 扣除当前卡密点数, 用于用户使用了某些特殊功能需要额外扣费的场景 (每次调用联网) */
	/* 参数：iFYICount：需要扣除的点数数量 */
	/* 参数：iSurplusFYI：扣除后剩下的点数，若未能扣除成功则该值表示为当前卡密的点数 */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError */
	inline bool  __stdcall __SP_Cloud_DeductFYI(__int64 iFYICount, OUT __int64* iSurplusFYI, OUT OPTIONAL int* iError);

	/* 描述: 云计算, 设置云计算操作系统版本标识 (登录之前使用) */
	/* 参数: szWinVer; 自定义操作系统版本标识, 如果为空, 则为内置逻辑获取操作系统版本 */
	bool  __stdcall __SP_Cloud_SetWinVer(char* szWinVer);

#if __USE_FUNC_PERFIX == 0

	/* 描述: 验证初始化 */
	/* 参数: szConnectKey; 连接密钥 打开服务端的独立软件管理界面 双击对应的软件位后可以找到 */
	/* 参数: iTimeout; send/recv超时时间(毫秒) */
	/* 参数: bIgnoreHint; 忽略加壳提示，默认为false即不忽略 */
	/* 返回: 参考云计算iError  */
	inline int  __stdcall SP_Verify_Init(const char* szConnectKey, int iTimeout, bool bIgnoreHint = false) { return __SP_Verify_Init(szConnectKey, iTimeout, bIgnoreHint); }

	/* 描述: 启用云计算PIC (不联网) */
	/* 云计算内使用PIC效率太低,所以默认禁用 */
	/* 调用该函数后,可在云计算类函数返回成功后使用相应的PIC end,如果为非法/破解调用则会崩溃 */
	/* 该函数调用一次即可 */
	/* 返回: 无  */
	inline void __stdcall SP_Cloud_PICEnable() { __SP_Cloud_PICEnable(); }

	/* 描述: 获取公告 (每次调用联网)*/
	/* 参数: pNotice; 接收公告 */
	/* 返回: iError; 云计算错误码/状态码 */
	inline int  __stdcall SP_Verify_GetNotice(OUT char* pNotice) { return  __SP_Verify_GetNotice(pNotice); }

	/* 描述: 验证 获取最新版本信息 (每次调用联网) */
	/* 参数: VersionInfo; 最新版本信息 */
	/* 返回: iError; 云计算错误码/状态码  */
	inline int  __stdcall SP_Verify_GetLastestVersionInfo(OUT st_SPUpdateVersionInfo* VersionInfo) { return  __SP_Verify_GetLastestVersionInfo(VersionInfo); }

	/* 描述: 验证, 获取服务端配置信息 (每次调用联网)*/
	/* 参数: pServerOption; 接收配置信息 */
	/* 返回: iError; 云计算错误码/状态码 */
	inline int  __stdcall SP_Verify_GetServerOption(OUT st_SPServerOption* pServerOption) { return  __SP_Verify_GetServerOption(pServerOption); }

	/* 描述: 验证, 拉黑当前机器的机器码 (每次调用联网)*/
	/* 返回: iError; 云计算错误码/状态码 */
	inline int  __stdcall SP_Verify_DisablePCSign() { return  __SP_Verify_DisablePCSign(); }

	/* 描述: 验证, 拉黑当前机器的IP (每次调用联网)*/
	/* 返回: iError; 云计算错误码/状态码 */
	inline int  __stdcall SP_Verify_DisableIP() { return  __SP_Verify_DisableIP(); }

	/* 描述: 验证, 反调试/检测调试器 (不联网)*/
	/* 返回: 是否被调试 */
	inline bool __stdcall SP_Verify_AntiDebugger() { return  __SP_Verify_AntiDebugger(); }

	/* 描述: 验证设置Socks5代理 默认不开启Socks5代理 由客户端直连服务端 */
	/* 参数: bEnable; 是否启用Socks5代理 如果在启用Socks5代理后需要临时禁用则传入false */
	/* 参数: szIP; Socks5代理IP地址/域名 */
	/* 参数: wPort; Socks5代理端口号 */
	/* 参数: szUsername; Socks5代理账号 没有则传入NULL */
	/* 参数: szPassword; Socks5代理密码 没有则传入NULL */
	inline void  __stdcall SP_Verify_SetSocks5(bool bEnable = true, const char* szHost = NULL, int wPort = 0, const char* szUsername = NULL, const char* szPassword = NULL) { return __SP_Verify_SetSocks5(bEnable, szHost, wPort, szUsername, szPassword); }

	/* 描述: 验证, 设置验证实际访问的IP或域名，可用于多线路，注意，服务端与客户端的链接密钥仍要保持一致,也需要在调用SP_Verify_Init后再使用 */
	/* 参数: szHost; IP或域名 */
	/* 参数: wPort; 端口 */
	inline void __stdcall SP_Verify_SetHost(const char* szHost, unsigned short wPort) { return __SP_Verify_SetHost(szHost, wPort); }

	/* 描述: 验证, 获取试用卡,服务器关闭试用则无效 (每次调用联网)*/
	/* 参数: szCard; 输出获取到的试用卡 */
	/* 返回: iError; 云计算错误码/状态码 */
	inline int  __stdcall SP_Verify_GetTrialCard(OUT char szCard[41]) { return __SP_Verify_GetTrialCard(szCard); }

	/* 描述: 验证 卡密登录 */
	/* 参数: szCard; 卡密 */
	/* 返回: 参考云计算iError  */
	inline int  __stdcall SP_Verify_CardLogin(const char* szCard) { return __SP_Verify_CardLogin(szCard); }

	/* 描述: 验证 账号密码登录 */
	/* 参数: szUsername; 账号 */
	/* 参数: szPassword; 密码 */
	/* 返回: 参考云计算iError  */
	inline int  __stdcall SP_Verify_UserLogin(const char* szUsername, const char* szPassword) { return __SP_Verify_UserLogin(szUsername, szPassword); }

	/* 描述: 验证 卡密解绑 (每次调用联网)*/
	/* 参数: szCard; 卡密 */
	/* 参数: UnBindInfo; 接收解绑返回信息(剩余解绑次数,解绑扣时等) */
	/* 返回: 参考云计算iError */
	inline int  __stdcall SP_Verify_CardUnbind(const char* szCard, OUT st_SPUnBindInfo* UnBindInfo) { return __SP_Verify_CardUnbind(szCard, UnBindInfo); }

	/* 描述: 验证 账号解绑 (每次调用联网)*/
	/* 参数: szCard; 卡密 */
	/* 参数: UnBindInfo; 接收解绑返回信息(剩余解绑次数,解绑扣时等) */
	/* 返回: 参考云计算iError */
	inline int  __stdcall SP_Verify_UserUnbind(const char* szUsername, const char* szPassword, OUT st_SPUnBindInfo* UnBindInfo) { return __SP_Verify_UserUnbind(szUsername, szPassword, UnBindInfo); }

	/* 描述: 验证 账号充值 */
	/* 参数: szUsername; 账号 */
	/* 参数: szRechargeCards; 充值卡 如需填写多个充值卡请用换行符拼接 */
	/* 参数: RechargeInfo; 接收充值返回信息 */
	/* 返回: 参考云计算iError  */
	inline int  __stdcall SP_Verify_UserRecharge(const char* szUsername, const char* szRechargeCards, OUT st_SPUserRechargeInfo* RechargeInfo) { return __SP_Verify_UserRecharge(szUsername, szRechargeCards, RechargeInfo); }

	/* 描述: 验证 账号注册 */
	/* 参数: szUsername; 账号 */
	/* 参数: szPassword; 密码 */
	/* 参数: szSuperPWD; 超级密码 */
	/* 参数: szRechargeCards; 充值卡 如需填写多个充值卡请用换行符拼接 */
	/* 返回: 参考云计算iError  */
	inline int  __stdcall SP_Verify_UserRegister(const char* szUsername, const char* szPassword, const char* szSuperPWD, const char* szRechargeCards) { return __SP_Verify_UserRegister(szUsername, szPassword, szSuperPWD, szRechargeCards); }

	/* 描述: 验证 账号改密 */
	/* 参数: szUsername; 账号 */
	/* 参数: szSuperPWD; 超级密码 */
	/* 参数: szNewPWD; 新密码 */
	/* 返回: 参考云计算iError  */
	inline int  __stdcall SP_Verify_UserChangePWD(const char* szUsername, const char* szSuperPWD, const char* szNewPWD) { return __SP_Verify_UserChangePWD(szUsername, szSuperPWD, szNewPWD); }

	/* 描述: 验证, 获取卡密当前所有在线客户端信息,需卡密有效 (每次调用联网)*/
	/* 返回的客户端信息会保存到库申请的空间 在下次调用CardQueryOnlineClient或UserQueryOnlineClient销毁*/
	/* 参数: szCard; 卡密 */
	/* 参数: pOnlineInfoHead; 在线客户端信息 */
	/* 返回: iError; 云计算错误码/状态码 */
	inline int  __stdcall SP_Verify_CardQueryOnlineClient(const char* szCard, OUT st_SPOnlineInfoHead* pOnlineInfoHead) { return __SP_Verify_CardQueryOnlineClient(szCard, pOnlineInfoHead); }

	/* 描述: 验证, 获取某个账号当前在线客户端信息,需账号密码有效 (每次调用联网)*/
	/* 返回的客户端信息会保存到库申请的空间 在下次调用CardQueryOnlineClient或UserQueryOnlineClient销毁*/
	/* 参数: szUsername; 账号 */
	/* 参数: szPassword; 密码 */
	/* 参数: pOnlineInfoHead; 在线客户端信息 */
	/* 返回: iError; 云计算错误码/状态码 */
	inline int  __stdcall SP_Verify_UserQueryOnlineClient(const char* szUsername, const char* szPassword, OUT st_SPOnlineInfoHead* pOnlineInfoHead) { return __SP_Verify_UserQueryOnlineClient(szUsername, szPassword, pOnlineInfoHead); }

	/* 描述: 验证, 关闭卡密其他在线连接,需卡密有效 (每次调用联网)*/
	/* 参数: szCard; 卡密 */
	/* 参数: iCID; 客户端ID 从SP_Verify_CardQueryOnlineClient函数可获得 */
	/* 返回: iError; 云计算错误码/状态码 */
	inline int  __stdcall SP_Verify_CardCloseOnlineByCID(const char* szCard, int iCID) { return __SP_Verify_CardCloseOnlineByCID(szCard, iCID); }

	/* 描述: 验证, 关闭账号其他在线连接,需账号密码有效 (每次调用联网)*/
	/* 参数: szUsername; 账号 */
	/* 参数: szPassword; 密码 */
	/* 参数: iCID; 客户端ID 从SP_Verify_UserQueryOnlineClient函数可获得 */
	/* 返回: iError; 云计算错误码/状态码 */
	inline int  __stdcall SP_Verify_UserCloseOnlineByCID(const char* szUsername, const char* szPassword, int iCID) { return __SP_Verify_UserCloseOnlineByCID(szUsername, szPassword, iCID); }


	/* 描述: 验证, 获取卡密已绑定的所有的机器码信息,需卡密有效 (每次调用联网)*/
	/* 返回的机器码信息会保存到库申请的空间 在下次调用CardQueryPCSigns或UserQueryPCSigns销毁*/
	/* 参数: szCard; 卡密 */
	/* 参数: pPCSignInfos; 已绑定机器码信息 */
	/* 返回: iError; 云计算错误码/状态码 */
	inline int  __stdcall SP_Verify_CardQueryPCSignInfos(const char* szCard, OUT st_SPQueryPCSignInfos* pPCSignInfos) { return __SP_Verify_CardQueryPCSignInfos(szCard, pPCSignInfos); }

	/* 描述: 验证, 获取账号已绑定的所有的机器信息,需账号密码有效 (每次调用联网)*/
	/* 返回的机器码信息会保存到库申请的空间 在下次调用CardQueryPCSigns或UserQueryPCSigns销毁*/
	/* 参数: szUsername; 账号 */
	/* 参数: szPassword; 密码 */
	/* 参数: pPCSignInfos; 已绑定机器码信息 */
	/* 返回: iError; 云计算错误码/状态码 */
	inline int  __stdcall SP_Verify_UserQueryPCSignInfos(const char* szUsername, const char* szPassword, OUT st_SPQueryPCSignInfos* pPCSignInfos) { return __SP_Verify_UserQueryPCSignInfos(szUsername, szPassword, pPCSignInfos);  }

	/* 描述: 验证, 移除卡密的某个已绑定机器码,需卡密有效 (每次调用联网)*/
	/* 参数: szCard; 卡密 */
	/* 参数: szUnbindPCSign; 欲移除的机器码 传入NULL或者0字符则表示为本机机器码 */
	/* 参数: bUnbindIP; 是否同时解除IP的绑定 */
	/* 返回: iError; 云计算错误码/状态码 */
	inline int  __stdcall SP_Verify_CardRemovePCSign(const char* szCard, const char* szUnbindPCSign, bool bUnbindIP) { return __SP_Verify_CardRemovePCSign(szCard, szUnbindPCSign, bUnbindIP);  }

	/* 描述: 验证, 关闭账号其他在线连接,需账号密码有效 (每次调用联网)*/
	/* 参数: szUsername; 账号 */
	/* 参数: szPassword; 密码 */
	/* 参数: szUnbindPCSign; 欲移除的机器码 传入NULL或者0字符则表示为本机机器码 */
	/* 参数: bUnbindIP; 是否同时解除IP的绑定 */
	/* 返回: iError; 云计算错误码/状态码 */
	inline int  __stdcall SP_Verify_UserRemovePCSign(const char* szUsername, const char* szPassword, const char* szUnbindPCSign, bool bUnbindIP) { return __SP_Verify_UserRemovePCSign(szUsername, szPassword, szUnbindPCSign, bUnbindIP);  }

	/* 描述: 验证, 通过账号的关联卡找回账号密码 (每次调用联网)*/
	/* 参数: szCard; 充值到账号里的卡密或者账号的主卡 */
	/* 参数: szUsername; 账号的账号名 */
	/* 参数: szPassword; 账号的密码 */
	/* 参数: szSuperPWD; 账号的超级密码 */
	/* 返回: iError; 云计算错误码/状态码 */
	inline int  __stdcall SP_Verify_UserRetrievePassword(const char* szCard, OUT char szUsername[33], OUT char szPassword[33], OUT char szSuperPWD[33]) { return __SP_Verify_UserRetrievePassword(szCard, szUsername, szPassword, szSuperPWD); }


	/* 描述: 查询是否已登录 原理是调用一次心跳包 (每次调用联网)*/
	/* 返回: 是否登录 */
	inline bool  __stdcall SP_Verify_IsLogin() { return __SP_Verify_IsLogin(); }

	/* 描述: 查询错误号的详细信息 (不联网)*/
	/* 参数: iError; 云计算错误码/状态码 */
	/* 参数: pMsg; 接收错误信息 */
	inline void  __stdcall SP_Verify_GetErrorMsg(int iError, OUT char* pMsg) { return __SP_Verify_GetErrorMsg(iError, pMsg); }

	/* 描述: 云计算请求 (每次调用联网) */
	/* 参数: dwCloudID; 云计算ID; (必须大于0) */
	/* 参数: pData; 云计算数据包指针 */
	/* 参数: dwLength; 云计算数据包长度 */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 云计算是否成功; 如果出错, 可参考iError */
	inline bool  __stdcall SP_CloudComputing(int dwCloudID, unsigned char* pData, int dwLength, int* iError) { return __SP_CloudComputing(dwCloudID, pData, dwLength, iError); }

	/* 描述: 云计算请求 (数据无加密) */
	/* 参数: dwCloudID; 云计算ID */
	/* 参数: pData; 云计算数据包指针 */
	/* 参数: dwLength; 云计算数据包长度 */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 云计算是否成功; 如果出错, 可参考iError */
	inline bool  __stdcall SP_CloudComputing_NoEncrypt(int dwCloudID, unsigned char* pData, int dwLength, int* iError) { return __SP_CloudComputing_NoEncrypt(dwCloudID, pData, dwLength, iError); }

	/* 描述: 云计算返回数据 */
	/* 参数: dwYunID; 云计算ID; (必须大于0) */
	/* 参数: pData; 存放读取内容的缓冲区 */
	/* 参数: dwLength; 要读取的长度 */
	/* 返回: 读取到的长度 */
	inline int  __stdcall SP_CloudResult(int dwCloudID, unsigned char* pData, int dwLength) { return __SP_CloudResult(dwCloudID, pData, dwLength); }

	/* 描述: 云计算, 返回数据总长度 */
	/* 参数: dwCloudID; 云计算ID; (必须大于0) */
	/* 返回: 总长度 */
	inline int  __stdcall SP_CloudResultLength(int dwCloudID) { return __SP_CloudResultLength(dwCloudID); }

	/* 描述: 云计算, 返回数据剩余未读长度 */
	/* 参数: dwCloudID; 云计算ID; (必须大于0) */
	/* 返回: 读取到的长度 */
	inline int  __stdcall SP_CloudResultRest(int dwCloudID) { return __SP_CloudResultRest(dwCloudID); }

	/* 描述: 云计算, 频率验证 (建议创建一条线程来频繁调用, 比如30秒调用一次) */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否验证成功; 如果出错, 可参考iError  */
	inline bool  __stdcall SP_Cloud_Beat(int* iError) { return __SP_Cloud_Beat(iError); }

	/* 描述: 云计算, 获取当前登陆卡密所属代理名 (每次调用联网) */
	/* 参数: szAgent[44] */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	inline bool  __stdcall SP_Cloud_GetCardAgent(char szAgent[44], int* iError) { return __SP_Cloud_GetCardAgent(szAgent, iError); }

	/* 描述: 云计算, 获取当前登陆卡密的卡类型 (每次调用联网) */
	/* 参数: szCardType[36] */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	inline bool  __stdcall SP_Cloud_GetCardType(char szCardType[36], int* iError) { return __SP_Cloud_GetCardType(szCardType, iError); }

	/* 描述: 云计算, 获取当前登陆卡密登录时记录的IP地址 (每次调用联网) */
	/* 参数: szIPAddress[44] */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	inline bool  __stdcall SP_Cloud_GetIPAddress(char szIPAddress[44], int* iError) { return __SP_Cloud_GetIPAddress(szIPAddress, iError); }

	/* 描述: 云计算, 获取当前登陆卡密的备注 (每次调用联网) */
	/* 参数: szRemarks[132] */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	inline bool  __stdcall SP_Cloud_GetRemarks(char szRemarks[132], int* iError) { return __SP_Cloud_GetRemarks(szRemarks, iError); }

	/* 描述: 云计算, 获取当前登陆卡密的创建时间戳 (每次调用联网) */
	/* 参数: iCreatedTimeStamp */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	inline bool  __stdcall SP_Cloud_GetCreatedTimeStamp(__int64* iCreatedTimeStamp, int* iError) { return __SP_Cloud_GetCreatedTimeStamp(iCreatedTimeStamp, iError); }

	/* 描述: 云计算, 获取当前登陆卡密的激活时间戳 (每次调用联网) */
	/* 参数: iActivatedTimeStamp */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	inline bool  __stdcall SP_Cloud_GetActivatedTimeStamp(__int64* iActivatedTimeStamp, int* iError) { return __SP_Cloud_GetActivatedTimeStamp(iActivatedTimeStamp, iError); }

	/* 描述: 云计算, 获取当前登陆卡密的过期时间戳 (每次调用联网) */
	/* 参数: iExpiredTimeStamp */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	inline bool  __stdcall SP_Cloud_GetExpiredTimeStamp(__int64* iExpiredTimeStamp, int* iError) { return __SP_Cloud_GetExpiredTimeStamp(iExpiredTimeStamp, iError); }

	/* 描述: 云计算, 获取当前登陆卡密的最后登录时间戳 (每次调用联网) */
	/* 参数: iLastLoginTimeStamp */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	inline bool  __stdcall SP_Cloud_GetLastLoginTimeStamp(__int64* iLastLoginTimeStamp, int* iError) { return __SP_Cloud_GetLastLoginTimeStamp(iLastLoginTimeStamp, iError); }

	/* 描述: 云计算, 获取当前登陆卡密的剩余点数 (每次调用联网) */
	/* 参数: iFYI */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	inline bool  __stdcall SP_Cloud_GetFYI(__int64* iFYI, int* iError) { return __SP_Cloud_GetFYI(iFYI, iError); }

	/* 描述: 云计算, 获取当前登陆卡密的多开数量属性值 (每次调用联网) */
	/* 参数: iNum */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	inline bool  __stdcall SP_Cloud_GetOpenMaxNum(int* iNum, int* iError) { return __SP_Cloud_GetOpenMaxNum(iNum, iError); }

	/* 描述: 云计算, 获取当前登陆卡密的绑定机器属性值 (每次调用联网) */
	/* 参数: iBind; 是否绑机, 1/0 */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	inline bool  __stdcall SP_Cloud_GetBind(int* iBind, int* iError) { return __SP_Cloud_GetBind(iBind, iError); }

	/* 描述: 云计算, 获取当前登陆卡密的换绑周期 (每次调用联网) */
	/* 参数: iBindTime; (秒) */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	inline bool  __stdcall SP_Cloud_GetBindTime(__int64* iBindTime, int* iError) { return __SP_Cloud_GetBindTime(iBindTime, iError); }

	/* 描述: 云计算, 获取当前登陆卡密的解绑扣除属性值 (每次调用联网) */
	/* 参数: iDeductSec; (秒) */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	inline bool  __stdcall SP_Cloud_GetUnBindDeductTime(__int64* iDeductSec, int* iError) { return __SP_Cloud_GetUnBindDeductTime(iDeductSec, iError); }

	/* 描述: 云计算, 获取当前登陆卡密的最多解绑次数属性值 (每次调用联网) */
	/* 参数: iNum */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	inline bool  __stdcall SP_Cloud_GetUnBindMaxNum(int* iNum, int* iError) { return __SP_Cloud_GetUnBindMaxNum(iNum, iError); }

	/* 描述: 云计算, 获取当前登陆卡密的累计解绑次数 (每次调用联网) */
	/* 参数: iCountTotal */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	inline bool  __stdcall SP_Cloud_GetUnBindCountTotal(int* iCountTotal, int* iError) { return __SP_Cloud_GetUnBindCountTotal(iCountTotal, iError); }

	/* 描述: 云计算, 获取当前登陆卡密的累计解绑扣除的时间 (每次调用联网) */
	/* 参数: iDeductTimeTotal; (秒) */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	inline bool  __stdcall SP_Cloud_GetUnBindDeductTimeTotal(__int64* iDeductTimeTotal, int* iError) { return __SP_Cloud_GetUnBindDeductTimeTotal(iDeductTimeTotal, iError); }

	/* 描述: 云计算, 移除当前云计算身份认证信息 (每次调用联网) */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 下线是否成功; 如果出错, 可参考iError  */
	/* 调用该函数后 程序会无法通过PIC校验 请提醒用户重新启动程序 */
	inline bool  __stdcall SP_Cloud_Offline(int* iError) { return __SP_Cloud_Offline(iError); }

	/* 描述: 云计算, 获取当前登陆的卡密 (登录成功后可用) */
	/* 参数: szCard */
	inline void  __stdcall SP_Cloud_GetCard(char szCard[41]) { __SP_Cloud_GetCard(szCard); }

	/* 描述: 云计算, 获取当前登陆的账号 (登录成功后可用) */
	/* 参数: szUser */
	inline void  __stdcall SP_Cloud_GetUser(OUT char szUser[33]) { __SP_Cloud_GetUser(szUser); }

	/* 描述: 云计算, 禁用当前登陆的卡密 (登录成功后可用) */
	/* 参数: iError; 云计算错误码/状态码 */
	inline void  __stdcall SP_Cloud_DisableCard(OUT OPTIONAL int* iError) { __SP_Cloud_DisableCard(iError); }

	/* 描述: 云计算, 获取当前客户端ID (登录成功后可用) */
	inline int  __stdcall SP_Cloud_GetCID() { return __SP_Cloud_GetCID(); }

	/* 描述: 云计算, 获取当前卡密在线客户端数量 (S登录成功后可用) */
	/* 参数: iError; 云计算错误码/状态码 */
	inline bool  __stdcall SP_Cloud_GetOnlineCount(int* iCount, int* iError) { return __SP_Cloud_GetOnlineCount(iCount, iError); }

	/* 描述: 云计算, 获取当前设备机器码 (每次调用联网) */
	/* 参数: szPCSign 输出机器码 */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	inline bool  __stdcall SP_Cloud_GetPCSign(char szPCSign[33], OUT OPTIONAL int* iError) { return __SP_Cloud_GetPCSign(szPCSign, iError); }
	
	/* 描述: 云计算, 扣除当前卡密点数, 用于用户使用了某些特殊功能需要额外扣费的场景 (每次调用联网) */
	/* 参数：iFYICount：需要扣除的点数数量 */
	/* 参数：iSurplusFYI：扣除后剩下的点数，若未能扣除成功则该值表示为当前卡密的点数 */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError */
	inline bool  __stdcall SP_Cloud_DeductFYI(__int64 iFYICount, OUT __int64* iSurplusFYI, OUT OPTIONAL int* iError) { return __SP_Cloud_DeductFYI(iFYICount, iSurplusFYI, iError); }

	/* 描述: 云计算, 设置云计算操作系统版本标识 (登录之前使用) */
	/* 参数: szWinVer; 自定义操作系统版本标识, 如果为空, 则为内置逻辑获取操作系统版本 */
	inline bool  __stdcall SP_Cloud_SetWinVer(char* szWinVer) { return __SP_Cloud_SetWinVer(szWinVer); }

	/* 描述: 云计算, 获取当前登陆卡密周期内的解绑次数 (每次调用联网) */
	/* 参数: iCount */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	inline bool  __stdcall SP_Cloud_GetUnBindCount(int* iCount, OUT OPTIONAL int* iError) { return __SP_Cloud_GetUnBindCount(iCount, iError); }

	/* 描述: 云计算, 获取频率验证总在线数量 (每次调用联网, 该功能需要在服务端 [独立软件管理] 开启) */
	/* 参数: iTotalCount; 总在线数量 */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	inline bool  __stdcall SP_Cloud_GetOnlineTotalCount(unsigned int* iTotalCount, OUT OPTIONAL int* iError) { return __SP_Cloud_GetOnlineTotalCount(iTotalCount, iError); }

	/* 描述: 云计算, 获取在线卡密数量 (每次调用联网, 该功能需要在服务端 [独立软件管理] 开启) */
	/* 参数: iTotalCount; 总在线数量 */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	/* 说明: */
	/*		比如有100张多开数量为10的卡密, 所有卡密都已登录占满多开数量 */
	/*		此时服务端拥有100*10=1000条在线链接 */
	/*		1000条在线链接中实际上是有100张在线卡密, 调用当前接口后, iTotalCount值则为100 */
	inline bool  __stdcall SP_Cloud_GetOnlineCardsCount(unsigned int* iTotalCount, OUT OPTIONAL int* iError) { return __SP_Cloud_GetOnlineCardsCount(iTotalCount, iError); }

	/* 描述: 云计算, 获取指定卡密在线链接数量 (每次调用联网) */
	/* 参数: szCard; 卡密; 填写NULL为当前登录云计算的卡密 */
	/* 参数: iTotalCount; 总在线数量 */
	/* 参数: iError; 云计算错误码/状态码 */
	/* 返回: 是否获取成功; 如果出错, 可参考iError  */
	/* 说明: */
	/*		比如有1张多开数量为10的卡密 */
	/*		此时用户使用这张卡密登录了3个客户端, 调用当前接口后, iTotalCount值则为3 */
	inline bool  __stdcall SP_Cloud_GetOnlineCountByCard(OPTIONAL const char* szCard, unsigned int* iTotalCount, OUT OPTIONAL int* iError) { return __SP_Cloud_GetOnlineCountByCard(szCard, iTotalCount, iError); }
#endif // __USE_FUNC_PERFIX == 0
}

#endif // !_SP_VERIFY_HEADER