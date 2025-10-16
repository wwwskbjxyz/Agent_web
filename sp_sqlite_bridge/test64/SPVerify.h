#ifndef _SP_VERIFY_HEADER
#define _SP_VERIFY_HEADER

#include <Windows.h>
#include <vector>
#include <memory>
#include "SPVerifyDefine.h"

//����lib
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
// �������
//

enum enum_SPErrorCode
{
	SP_NOERROR = 0,			/* ���� */
	SP_NOINIT = -1,			/* û��������ʼ�� */
	SP_WSAFAILED = -2,		/* WSAStartup��������ʧ�� */
	SP_CONNECTFAILED = -3, /* ����3�κ�, ����ͨѶ��Ȼ���� */
	SP_DATAERROR = -4,		/* ����3�κ�, ���ݰ���Ȼ�쳣 */
	SP_NOLOADCLOUDDLL = -5, /* δ�����Ƽ��������Ƽ�����δ����SP_CloudComputing_Callback���� */
	SP_FAILEDWRITE = -6,	/* �ṩ��ָ�벻��д */
	SP_EXPIREDTOKEN = -15,	/* �����֤��Ϣ�ѹ���!�����µ�¼ */
	SP_UNKNOWNERRROR = -16,	/* δ֪���� */
	SP_INVALIDCARD = -21,	/* ����/�˺�������Ч����� */
	SP_EXPIREDCARD = -22,	/* ����/�˺��ѹ��� */
	SP_BANNEDCARD = -23,	/* ����/�˺ű���ͣ */
	SP_INVALIDAGENT = -24,	/* ��������Ч */
	SP_BANNEDAGENT = -25,	/* �����̱�ͣ�� */
	SP_MAXONLINE = -26,		/* �Ѵﵽ����������� */
	SP_OFFLINE = -27,		/* ��ǰ�ͻ��������֤��Ϣ�ѱ��Ƴ��������Ǳ����������������߻��������ӵ�¼�����ߣ� */
	SP_ANOTHERUSERUNBIND = -28, /* �������������Ͻ�󣬵�ǰ���������� */
	SP_RESERVE1 = -29, /* ��������ʱ���� */
	SP_QUERYAGENTEXCEPTION = -30, /* �������쳣 */
	SP_INVALIDPARAM = -31, /* ����������󣨸�ʽ���򳬹��޶ȣ� */
	SP_FYINOTENOUGH = -32, /* ��ǰ����ʣ���������(�۳�������ʣ�����) */
	SP_RESERVE2 = -33, /* ��������ʱ���� */
	SP_NOACTIVATEDCARD = -38,/* ���ܻ�δ���� */
	SP_LOGINEXCEPTION = -39,/* �����쳣״�����޷���¼ */
	SP_FAILEDACTIVATECARD = -40,/* �޷������ */
	SP_BINDMSGDIFF = -41,	/* ��ǰ�豸��Ϣ�뿨�Ű���Ϣ��һ�� */
	SP_FAILEDGETCARD = -42,	/* ��ȡ��������ʱ���� */
	SP_NOBIND = -43,		/* ����/�˺�û�����ð���Ϣ��ǰ�����뿨��/�˺ŵİ���Ϣһ�� */
	SP_MAXUNBINDCOUNT = -44,/* �Ѵﵽ�������� */
	SP_NOENOUGHTIME = -45,	/* ʣ��ʱ�䲻���Խ�� */
	SP_FAILEDUNBIND = -46,	/* ���ʧ�� */
	SP_UNBINDEXCEPTION = -47, /* ���Խ��ʱ�������� */
	SP_INVALIDUSERNAME = -48, /* �˺�����ռ�û���Ч */
	SP_INVALIDPWD = -49,	 /* ������Ч */
	SP_INVALIDRECHARGECARD = -50, /* ��ֵ����Ч */
	SP_INVALIDSPWD = -51,	/* ����������Ч����� */
	SP_OPERATEEXCEPTION = -52,  /* ����ʱ���ִ��� */
	SP_TRIALMSGDIFF = -53, /* ���ÿ�����Ϣ���¼������һ�� */
	SP_APPLIEDTRIAL = -54, /* �Ѿ���������� */
	SP_TRIALERROR = -55,   /* ��������ʱ���� */
	SP_EXPIREDTRIAL = -56, /* �����ѵ��� */
	SP_UNBINDTRIAL = -57, /* ���ÿ����ɽ�� */
	SP_CLOSEFAILED = -58, /* �رտͻ���ʧ�� */
	SP_DISABLETRIAL = -60,   /* ��ֹ���� */
	SP_DISABLELOGIN = -61,	/* ��ֹ��¼ */
	SP_DISABLEREGISTER = -62,	/* ��ֹע�� */
	SP_DISABLERECHARGE = -63,	/* ��ֹ��ֵ */
	SP_DISABLEMIXCARDRECHARGE = -64,	/* ��ֹ��ֵ��ͬ�����͵Ŀ��� */
	SP_DISABLEMIXAGENTRECHARGE = -65,	/* ��ֹ��ֵ��ͬ����Ŀ��� */
	SP_NOMATCHPCSIGN = -66, /* �����ֻ������ǰ��¼�豸��ͬ�Ļ����� */
	SP_NOEXISTPCSIGN = -67, /* �����Ļ����벻���� */
	SP_TRIALCARDUSEDFORRECHARGING = -68, /* �޷������ÿ����ڳ�ֵ */
	SP_ACTIVATEDCARDUSEDFORRECHARGING = -69, /* �޷����Ѽ���Ŀ����ڳ�ֵ */
	SP_USEDCARD = -70, /* ��ֵ���ѱ�ʹ�� */
	SP_TRIALCARDMUSTREGISTEREDSEPARATELY = -71, /* ���ÿ��޷�����ͨ�����ע�� */
};


//
// �����Ϣ
//

struct st_SPUnBindInfo
{
	int RemainUnBindCount; //ʣ�������
	int RemainFreeCount; //ʣ����ѽ�����
	__int64 UnBindLimitTime; //���ʱ�� ��λ:��
	__int64 UnBindDeductTime; //����ʱ,��ѽ��ʱ��ֵΪ0 ��λ:��
};


//
// ������Ϣ
//

struct st_SPUpdateVersionInfo
{
	int	UpdateVer;		// ���°汾��
	BOOL UpdateForce;	// �Ƿ�ǿ�Ƹ���
	BOOL UpdateDirectUrl;// �Ƿ�ֱ������
	char UpdateUrl[2049];	// �������ص�ַ
	char UpdateRunExe[101];	// ���º������е�exe
	char UpdateRunCmd[129];	// ���º������е�exe�ĸ��Ӳ���
};

//
// ��ֵ��Ϣ
//

struct st_SPUserRechargeInfo
{
	__int64	nOldExpiredTimeStamp;//	�ɵĿ��ܹ���ʱ���;
	__int64	nNewExpiredTimeStamp;//	�µĿ��ܹ���ʱ���;
	__int64	nOldCount;			// �ɵĳ�ֵ������;
	__int64	nNewCount;			// �µĳ�ֵ������;
	int		nRechargeCount;		// ��ֵ�Ŀ��ܸ���;
};

//
// ������Ϣ
//
struct st_SPServerOption {
	BOOL DisableTrial;		// TRUEΪ��ֹ����
	BOOL DisableLogin;		// TRUEΪ��ֹ�����¼
	BOOL DisableRegister;	// TRUEΪ��ֹ���ע��
	BOOL DisableRecharge;	// TRUEΪ��ֹ�����ֵ
	BOOL DisableGetCountInfo;	// TRUEΪ��ֹ���ʹ�� [��ȡƵ����֤�������� SP_Cloud_GetOnlineTotalCount]��[��ȡ���߿����� SP_Cloud_GetOnlineCardsCount]
	int		Reserved[15];//			�����ֶ�;
};

//
// �ͻ���������Ϣ
//
struct st_SPOnlineInfo {
	int		nCID;//�ͻ���ID
	const char* szComputerName;//�ͻ��˵ĵ�����
	const char* szWinVer;//�ͻ��˵�windows�汾
	__int64		LoginTS;//�ͻ��˵ĵ�¼ʱ��
};

//
// �ͻ���������Ϣͷ��
//
struct st_SPOnlineInfoHead {
	int		nCount;//���߿ͻ��˸���
	const st_SPOnlineInfo* Info;//�ͻ���������Ϣָ�룬���Ը��ݸ�������
};

/* ����: �ͻ����Ѱ󶨻�����Ϣ */
struct st_SPPCSignInfo {
	__int64	BindTS;//��ʱ���
	const char* szPCSign;//������
	const char* szComputerName;//�����ĵ�����
	const char* szWinVer;//������windows�汾
	const char* szRemark;//�����ı�ע ����
	__int64	LastLoginTS;//����������¼ʱ���
};

/* ����: �ͻ����Ѱ󶨻�����Ϣͷ */
struct st_SPPCSignInfosHead {
	int		nCount;//�󶨻����ĸ���
	const st_SPPCSignInfo* Info;//�Ѱ󶨻�����Ϣָ��
};

/* ����: �ͻ����Ѱ󶨻������� */
struct st_SPQueryPCSignInfos {
	BOOL bBindIP; // �Ƿ����IP
	BOOL bLockBindPCSign; // �Ƿ�������Ƴ�����Ļ����� ��ΪTRUE���޷��Ƴ���������������Ѱ󶨻�����
	uint32_t	RestCount;// ������ʣ�����
	__int64		RefreshUnbindRemainSeconds;// ����; `����[�����������ʱ��]��ʣ��xxx��xxʱxx��
	st_SPPCSignInfosHead* PCSignInfosHead; // �ͻ����Ѱ󶨻�����Ϣͷ
};

//
// ����
//

extern "C" {

	/* ����: ��֤��ʼ�� */
	/* ����: szConnectKey; ������Կ �򿪷���˵Ķ������������� ˫����Ӧ�����λ������ҵ� */
	/* ����: iTimeout; send/recv��ʱʱ��(����) */
	/* ����: bIgnoreHint; ���Լӿ���ʾ��Ĭ��Ϊfalse�������� */
	/* ����: �ο��Ƽ���iError  */
	int  __stdcall __SP_Verify_Init(const char* szConnectKey, int iTimeout, bool bIgnoreHint = false);

	/* ����: �����Ƽ���PIC (������) */
	/* �Ƽ�����ʹ��PICЧ��̫��,����Ĭ�Ͻ��� */
	/* ���øú�����,�����Ƽ����ຯ�����سɹ���ʹ����Ӧ��PIC end,���Ϊ�Ƿ�/�ƽ���������� */
	/* �ú�������һ�μ��� */
	/* ����: ��  */
	void __stdcall __SP_Cloud_PICEnable();

	/* ����: ��ȡ���� (ÿ�ε�������)*/
	/* ����: pNotice; ���չ��� */
	/* ����: iError; �Ƽ��������/״̬�� */
	int  __stdcall __SP_Verify_GetNotice(OUT char* pNotice);

	/* ����: ��֤ ��ȡ���°汾��Ϣ (ÿ�ε�������) */
	/* ����: VersionInfo; ���°汾��Ϣ */
	/* ����: iError; �Ƽ��������/״̬��  */
	int  __stdcall __SP_Verify_GetLastestVersionInfo(OUT st_SPUpdateVersionInfo* VersionInfo);

	/* ����: ��֤, ��ȡ�����������Ϣ (ÿ�ε�������)*/
	/* ����: pServerOption; ����������Ϣ */
	/* ����: iError; �Ƽ��������/״̬�� */
	int  __stdcall __SP_Verify_GetServerOption(OUT st_SPServerOption* pServerOption);

	/* ����: ��֤, ���ڵ�ǰ�����Ļ����� (ÿ�ε�������)*/
	/* ����: iError; �Ƽ��������/״̬�� */
	int  __stdcall __SP_Verify_DisablePCSign();

	/* ����: ��֤, ���ڵ�ǰ������IP (ÿ�ε�������)*/
	/* ����: iError; �Ƽ��������/״̬�� */
	int  __stdcall __SP_Verify_DisableIP();

	/* ����: ��֤, ������/�������� (������)*/
	/* ����: �Ƿ񱻵��� */
	bool __stdcall __SP_Verify_AntiDebugger();

	/* ����: ��֤����Socks5���� Ĭ�ϲ�����Socks5���� �ɿͻ���ֱ������� */
	/* ����: bEnable; �Ƿ�����Socks5���� ���������Socks5�������Ҫ��ʱ��������false */
	/* ����: szIP; Socks5����IP��ַ/���� */
	/* ����: wPort; Socks5����˿ں� */
	/* ����: szUsername; Socks5�����˺� û������NULL */
	/* ����: szPassword; Socks5�������� û������NULL */
	void  __stdcall __SP_Verify_SetSocks5(bool bEnable = true, const char* szHost = NULL, int wPort = 0, const char* szUsername = NULL, const char* szPassword = NULL);

	/* ����: ��֤, ������֤ʵ�ʷ��ʵ�IP�������������ڶ���·��ע�⣬�������ͻ��˵�������Կ��Ҫ����һ��,Ҳ��Ҫ�ڵ���SP_Verify_Init����ʹ�� */
	/* ����: szHost; IP������ */
	/* ����: wPort; �˿� */
	void __stdcall __SP_Verify_SetHost(const char* szHost, unsigned short wPort);

	/* ����: ��֤, ��ȡ���ÿ�,�������ر���������Ч (ÿ�ε�������)*/
	/* ����: szCard; �����ȡ�������ÿ� */
	/* ����: iError; �Ƽ��������/״̬�� */
	int  __stdcall __SP_Verify_GetTrialCard(OUT char szCard[41]);

	/* ����: ��֤ ���ܵ�¼ */
	/* ����: szCard; ���� */
	/* ����: �ο��Ƽ���iError  */
	int  __stdcall __SP_Verify_CardLogin(const char* szCard);

	/* ����: ��֤ �˺������¼ */
	/* ����: szUsername; �˺� */
	/* ����: szPassword; ���� */
	/* ����: �ο��Ƽ���iError  */
	int  __stdcall __SP_Verify_UserLogin(const char* szUsername, const char* szPassword);

	/* ����: ��֤ ���ܽ�� (ÿ�ε�������)*/
	/* ����: szCard; ���� */
	/* ����: UnBindInfo; ���ս�󷵻���Ϣ(ʣ�������,����ʱ��) */
	/* ����: �ο��Ƽ���iError */
	int  __stdcall __SP_Verify_CardUnbind(const char* szCard, OUT st_SPUnBindInfo* UnBindInfo);

	/* ����: ��֤ �˺Ž�� (ÿ�ε�������)*/
	/* ����: szCard; ���� */
	/* ����: UnBindInfo; ���ս�󷵻���Ϣ(ʣ�������,����ʱ��) */
	/* ����: �ο��Ƽ���iError */
	int  __stdcall __SP_Verify_UserUnbind(const char* szUsername, const char* szPassword, OUT st_SPUnBindInfo* UnBindInfo);

	/* ����: ��֤ �˺ų�ֵ */
	/* ����: szUsername; �˺� */
	/* ����: szRechargeCards; ��ֵ�� ������д�����ֵ�����û��з�ƴ�� */
	/* ����: RechargeInfo; ���ճ�ֵ������Ϣ */
	/* ����: �ο��Ƽ���iError  */
	int  __stdcall __SP_Verify_UserRecharge(const char* szUsername, const char* szRechargeCards, OUT st_SPUserRechargeInfo* RechargeInfo);

	/* ����: ��֤ �˺�ע�� */
	/* ����: szUsername; �˺� */
	/* ����: szPassword; ���� */
	/* ����: szSuperPWD; �������� */
	/* ����: szRechargeCards; ��ֵ�� ������д�����ֵ�����û��з�ƴ�� */
	/* ����: �ο��Ƽ���iError  */
	int  __stdcall __SP_Verify_UserRegister(const char* szUsername, const char* szPassword, const char* szSuperPWD, const char* szRechargeCards);

	/* ����: ��֤ �˺Ÿ��� */
	/* ����: szUsername; �˺� */
	/* ����: szSuperPWD; �������� */
	/* ����: szNewPWD; ������ */
	/* ����: �ο��Ƽ���iError  */
	int  __stdcall __SP_Verify_UserChangePWD(const char* szUsername, const char* szSuperPWD, const char* szNewPWD);

	/* ����: ��֤, ��ȡ���ܵ�ǰ�������߿ͻ�����Ϣ,�迨����Ч (ÿ�ε�������)*/
	/* ���صĿͻ�����Ϣ�ᱣ�浽������Ŀռ� ���´ε���CardQueryOnlineClient��UserQueryOnlineClient����*/
	/* ����: szCard; ���� */
	/* ����: pOnlineInfoHead; ���߿ͻ�����Ϣ */
	/* ����: iError; �Ƽ��������/״̬�� */
	int  __stdcall __SP_Verify_CardQueryOnlineClient(const char* szCard, OUT st_SPOnlineInfoHead* pOnlineInfoHead);

	/* ����: ��֤, ��ȡĳ���˺ŵ�ǰ���߿ͻ�����Ϣ,���˺�������Ч (ÿ�ε�������)*/
	/* ���صĿͻ�����Ϣ�ᱣ�浽������Ŀռ� ���´ε���CardQueryOnlineClient��UserQueryOnlineClient����*/
	/* ����: szUsername; �˺� */
	/* ����: szPassword; ���� */
	/* ����: pOnlineInfoHead; ���߿ͻ�����Ϣ */
	/* ����: iError; �Ƽ��������/״̬�� */
	int  __stdcall __SP_Verify_UserQueryOnlineClient(const char* szUsername, const char* szPassword, OUT st_SPOnlineInfoHead* pOnlineInfoHead);

	/* ����: ��֤, �رտ���������������,�迨����Ч (ÿ�ε�������)*/
	/* ����: szCard; ���� */
	/* ����: iCID; �ͻ���ID ��SP_Verify_CardQueryOnlineClient�����ɻ�� */
	/* ����: iError; �Ƽ��������/״̬�� */
	int  __stdcall __SP_Verify_CardCloseOnlineByCID(const char* szCard, int iCID);

	/* ����: ��֤, �ر��˺�������������,���˺�������Ч (ÿ�ε�������)*/
	/* ����: szUsername; �˺� */
	/* ����: szPassword; ���� */
	/* ����: iCID; �ͻ���ID ��SP_Verify_UserQueryOnlineClient�����ɻ�� */
	/* ����: iError; �Ƽ��������/״̬�� */
	int  __stdcall __SP_Verify_UserCloseOnlineByCID(const char* szUsername, const char* szPassword, int iCID);

	/* ����: ��֤, ��ȡ�����Ѱ󶨵����еĻ�������Ϣ,�迨����Ч (ÿ�ε�������)*/
	/* ���صĻ�������Ϣ�ᱣ�浽������Ŀռ� ���´ε���CardQueryPCSigns��UserQueryPCSigns����*/
	/* ����: szCard; ���� */
	/* ����: pPCSignInfos; �Ѱ󶨻�������Ϣ */
	/* ����: iError; �Ƽ��������/״̬�� */
	int  __stdcall __SP_Verify_CardQueryPCSignInfos(const char* szCard, OUT st_SPQueryPCSignInfos* pPCSignInfos);

	/* ����: ��֤, ��ȡ�˺��Ѱ󶨵����еĻ�����Ϣ,���˺�������Ч (ÿ�ε�������)*/
	/* ���صĻ�������Ϣ�ᱣ�浽������Ŀռ� ���´ε���CardQueryPCSigns��UserQueryPCSigns����*/
	/* ����: szUsername; �˺� */
	/* ����: szPassword; ���� */
	/* ����: pPCSignInfos; �Ѱ󶨻�������Ϣ */
	/* ����: iError; �Ƽ��������/״̬�� */
	int  __stdcall __SP_Verify_UserQueryPCSignInfos(const char* szUsername, const char* szPassword, OUT st_SPQueryPCSignInfos* pPCSignInfos);

	/* ����: ��֤, �Ƴ����ܵ�ĳ���Ѱ󶨻�����,�迨����Ч (ÿ�ε�������)*/
	/* ����: szCard; ���� */
	/* ����: szUnbindPCSign; ���Ƴ��Ļ����� ����NULL����0�ַ����ʾΪ���������� */
	/* ����: bUnbindIP; �Ƿ�ͬʱ���IP�İ� */
	/* ����: iError; �Ƽ��������/״̬�� */
	int  __stdcall __SP_Verify_CardRemovePCSign(const char* szCard, const char* szUnbindPCSign, bool bUnbindIP);

	/* ����: ��֤, �ر��˺�������������,���˺�������Ч (ÿ�ε�������)*/
	/* ����: szUsername; �˺� */
	/* ����: szPassword; ���� */
	/* ����: szUnbindPCSign; ���Ƴ��Ļ����� ����NULL����0�ַ����ʾΪ���������� */
	/* ����: bUnbindIP; �Ƿ�ͬʱ���IP�İ� */
	/* ����: iError; �Ƽ��������/״̬�� */
	int  __stdcall __SP_Verify_UserRemovePCSign(const char* szUsername, const char* szPassword, const char* szUnbindPCSign, bool bUnbindIP);

	/* ����: ��֤, ͨ���˺ŵĹ������һ��˺����� (ÿ�ε�������)*/
	/* ����: szCard; ��ֵ���˺���Ŀ��ܻ����˺ŵ����� */
	/* ����: szUsername; �˺ŵ��˺��� */
	/* ����: szPassword; �˺ŵ����� */
	/* ����: szSuperPWD; �˺ŵĳ������� */
	/* ����: iError; �Ƽ��������/״̬�� */
	int  __stdcall __SP_Verify_UserRetrievePassword(const char* szCard, OUT char szUsername[33], OUT char szPassword[33], OUT char szSuperPWD[33]);

	/* ����: ��ѯ�Ƿ��ѵ�¼ ԭ���ǵ���һ�������� (ÿ�ε�������)*/
	/* ����: �Ƿ��¼ */
	bool  __stdcall __SP_Verify_IsLogin();

	/* ����: ��ѯ����ŵ���ϸ��Ϣ (������)*/
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: pMsg; ���մ�����Ϣ */
	void  __stdcall __SP_Verify_GetErrorMsg(int iError, OUT char* pMsg);

	/* ����: �Ƽ������� (ÿ�ε�������) */
	/* ����: dwCloudID; �Ƽ���ID; (�������0) */
	/* ����: pData; �Ƽ������ݰ�ָ�� */
	/* ����: dwLength; �Ƽ������ݰ����� */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƽ����Ƿ�ɹ�; �������, �ɲο�iError */
	bool  __stdcall __SP_CloudComputing(int dwCloudID, unsigned char* pData, int dwLength, int* iError);

	/* ����: �Ƽ������� (�����޼���) */
	/* ����: dwCloudID; �Ƽ���ID */
	/* ����: pData; �Ƽ������ݰ�ָ�� */
	/* ����: dwLength; �Ƽ������ݰ����� */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƽ����Ƿ�ɹ�; �������, �ɲο�iError */
	bool  __stdcall __SP_CloudComputing_NoEncrypt(int dwCloudID, unsigned char* pData, int dwLength, int* iError);

	/* ����: �Ƽ��㷵������ */
	/* ����: dwYunID; �Ƽ���ID; (�������0) */
	/* ����: pData; ��Ŷ�ȡ���ݵĻ����� */
	/* ����: dwLength; Ҫ��ȡ�ĳ��� */
	/* ����: ��ȡ���ĳ��� */
	int  __stdcall __SP_CloudResult(int dwCloudID, unsigned char* pData, int dwLength);

	/* ����: �Ƽ���, ���������ܳ��� */
	/* ����: dwCloudID; �Ƽ���ID; (�������0) */
	/* ����: �ܳ��� */
	int  __stdcall __SP_CloudResultLength(int dwCloudID);

	/* ����: �Ƽ���, ��������ʣ��δ������ */
	/* ����: dwCloudID; �Ƽ���ID; (�������0) */
	/* ����: ��ȡ���ĳ��� */
	int  __stdcall __SP_CloudResultRest(int dwCloudID);

	/* ����: �Ƽ���, Ƶ����֤ (���鴴��һ���߳���Ƶ������, ����30�����һ��) */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ���֤�ɹ�; �������, �ɲο�iError  */
	bool  __stdcall __SP_Cloud_Beat(int* iError);

	/* ����: �Ƽ���, ��ȡ��ǰ��½�������������� (ÿ�ε�������) */
	/* ����: szAgent[44] */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	bool  __stdcall __SP_Cloud_GetCardAgent(char szAgent[44], int* iError);

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵĿ����� (ÿ�ε�������) */
	/* ����: szCardType[36] */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	bool  __stdcall __SP_Cloud_GetCardType(char szCardType[36], int* iError);

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵ�¼ʱ��¼��IP��ַ (ÿ�ε�������) */
	/* ����: szIPAddress[44] */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	bool  __stdcall __SP_Cloud_GetIPAddress(char szIPAddress[44], int* iError);

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵı�ע (ÿ�ε�������) */
	/* ����: szRemarks[132] */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	bool  __stdcall __SP_Cloud_GetRemarks(char szRemarks[132], int* iError);

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵĴ���ʱ��� (ÿ�ε�������) */
	/* ����: iCreatedTimeStamp */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	bool  __stdcall __SP_Cloud_GetCreatedTimeStamp(__int64* iCreatedTimeStamp, int* iError);

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵļ���ʱ��� (ÿ�ε�������) */
	/* ����: iActivatedTimeStamp */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	bool  __stdcall __SP_Cloud_GetActivatedTimeStamp(__int64* iActivatedTimeStamp, int* iError);

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵĹ���ʱ��� (ÿ�ε�������) */
	/* ����: iExpiredTimeStamp */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	bool  __stdcall __SP_Cloud_GetExpiredTimeStamp(__int64* iExpiredTimeStamp, int* iError);

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵ�����¼ʱ��� (ÿ�ε�������) */
	/* ����: iLastLoginTimeStamp */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	bool  __stdcall __SP_Cloud_GetLastLoginTimeStamp(__int64* iLastLoginTimeStamp, int* iError);

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵ�ʣ����� (ÿ�ε�������) */
	/* ����: iFYI */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	bool  __stdcall __SP_Cloud_GetFYI(__int64* iFYI, int* iError);

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵĶ࿪��������ֵ (ÿ�ε�������) */
	/* ����: iNum */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	bool  __stdcall __SP_Cloud_GetOpenMaxNum(int* iNum, int* iError);

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵİ󶨻�������ֵ (ÿ�ε�������) */
	/* ����: iBind; �Ƿ���, 1/0 */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	bool  __stdcall __SP_Cloud_GetBind(int* iBind, int* iError);

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵĻ������� (ÿ�ε�������) */
	/* ����: iBindTime; (��) */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	bool  __stdcall __SP_Cloud_GetBindTime(__int64* iBindTime, int* iError);

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵĽ��۳�����ֵ (ÿ�ε�������) */
	/* ����: iDeductSec; (��) */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	bool  __stdcall __SP_Cloud_GetUnBindDeductTime(__int64* iDeductSec, int* iError);

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵ�������������ֵ (ÿ�ε�������) */
	/* ����: iNum */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	bool  __stdcall __SP_Cloud_GetUnBindMaxNum(int* iNum, int* iError);

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵ��ۼƽ����� (ÿ�ε�������) */
	/* ����: iCountTotal */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	bool  __stdcall __SP_Cloud_GetUnBindCountTotal(int* iCountTotal, int* iError);

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵ��ۼƽ��۳���ʱ�� (ÿ�ε�������) */
	/* ����: iDeductTimeTotal; (��) */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	bool  __stdcall __SP_Cloud_GetUnBindDeductTimeTotal(__int64* iDeductTimeTotal, int* iError);

	/* ����: �Ƽ���, ��ȡ��ǰ��½���������ڵĽ����� (ÿ�ε�������) */
	/* ����: iCount */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	bool  __stdcall __SP_Cloud_GetUnBindCount(int* iCount, OUT OPTIONAL int* iError);

	/* ����: �Ƽ���, ��ȡƵ����֤���������� (ÿ�ε�������, �ù�����Ҫ�ڷ���� [�����������] ����) */
	/* ����: iTotalCount; ���������� */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	bool  __stdcall __SP_Cloud_GetOnlineTotalCount(unsigned int* iTotalCount, OUT OPTIONAL int* iError);

	/* ����: �Ƽ���, ��ȡ���߿������� (ÿ�ε�������, �ù�����Ҫ�ڷ���� [�����������] ����) */
	/* ����: iOnlineCardsCount; ���߿������� */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	/* ˵��: */
	/*		������100�Ŷ࿪����Ϊ10�Ŀ���, ���п��ܶ��ѵ�¼ռ���࿪���� */
	/*		��ʱ�����ӵ��100*10=1000���������� */
	/*		1000������������ʵ��������100�����߿���, ���õ�ǰ�ӿں�, iTotalCountֵ��Ϊ100 */
	bool  __stdcall __SP_Cloud_GetOnlineCardsCount(unsigned int* iOnlineCardsCount, OUT OPTIONAL int* iError);

	/* ����: �Ƽ���, ��ȡָ������������������ (ÿ�ε�������) */
	/* ����: szCard; ����; ��дNULLΪ��ǰ��¼�Ƽ���Ŀ��� */
	/* ����: iOnlineCount; �������� */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	/* ˵��: */
	/*		������1�Ŷ࿪����Ϊ10�Ŀ��� */
	/*		��ʱ�û�ʹ�����ſ��ܵ�¼��3���ͻ���, ���õ�ǰ�ӿں�, iTotalCountֵ��Ϊ3 */
	bool  __stdcall __SP_Cloud_GetOnlineCountByCard(OPTIONAL const char* szCard, unsigned int* iOnlineCount, OUT OPTIONAL int* iError);

	/* ����: �Ƽ���, �Ƴ���ǰ�Ƽ��������֤��Ϣ (ÿ�ε�������) */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �����Ƿ�ɹ�; �������, �ɲο�iError  */
	/* ���øú����� ������޷�ͨ��PICУ�� �������û������������� */
	bool  __stdcall __SP_Cloud_Offline(int* iError);

	/* ����: �Ƽ���, ��ȡ��ǰ��½�Ŀ��� (��¼�ɹ������) */
	/* ����: szCard */
	void  __stdcall __SP_Cloud_GetCard(OUT char szCard[41]);

	/* ����: �Ƽ���, ��ȡ��ǰ��½���˺� (��¼�ɹ������) */
	/* ����: szUser */
	void  __stdcall __SP_Cloud_GetUser(OUT char szUser[33]);

	/* ����: �Ƽ���, ���õ�ǰ��½�Ŀ��� (��¼�ɹ������) */
	/* ����: iError; �Ƽ��������/״̬�� */
	void  __stdcall __SP_Cloud_DisableCard(OUT OPTIONAL int* iError);

	/* ����: �Ƽ���, ��ȡ��ǰ�ͻ���ID (��¼�ɹ������) */
	int  __stdcall __SP_Cloud_GetCID();

	/* ����: �Ƽ���, ��ȡ��ǰ�������߿ͻ������� (S��¼�ɹ������) */
	/* ����: iError; �Ƽ��������/״̬�� */
	bool  __stdcall __SP_Cloud_GetOnlineCount(int* iCount, int* iError);

	/* ����: �Ƽ���, ��ȡ��ǰ�豸������ (ÿ�ε�������) */
	/* ����: szPCSign ��������� */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	bool  __stdcall __SP_Cloud_GetPCSign(char szPCSign[33], OUT OPTIONAL int* iError);

	/* ����: �Ƽ���, �۳���ǰ���ܵ���, �����û�ʹ����ĳЩ���⹦����Ҫ����۷ѵĳ��� (ÿ�ε�������) */
	/* ������iFYICount����Ҫ�۳��ĵ������� */
	/* ������iSurplusFYI���۳���ʣ�µĵ�������δ�ܿ۳��ɹ����ֵ��ʾΪ��ǰ���ܵĵ��� */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError */
	inline bool  __stdcall __SP_Cloud_DeductFYI(__int64 iFYICount, OUT __int64* iSurplusFYI, OUT OPTIONAL int* iError);

	/* ����: �Ƽ���, �����Ƽ������ϵͳ�汾��ʶ (��¼֮ǰʹ��) */
	/* ����: szWinVer; �Զ������ϵͳ�汾��ʶ, ���Ϊ��, ��Ϊ�����߼���ȡ����ϵͳ�汾 */
	bool  __stdcall __SP_Cloud_SetWinVer(char* szWinVer);

#if __USE_FUNC_PERFIX == 0

	/* ����: ��֤��ʼ�� */
	/* ����: szConnectKey; ������Կ �򿪷���˵Ķ������������� ˫����Ӧ�����λ������ҵ� */
	/* ����: iTimeout; send/recv��ʱʱ��(����) */
	/* ����: bIgnoreHint; ���Լӿ���ʾ��Ĭ��Ϊfalse�������� */
	/* ����: �ο��Ƽ���iError  */
	inline int  __stdcall SP_Verify_Init(const char* szConnectKey, int iTimeout, bool bIgnoreHint = false) { return __SP_Verify_Init(szConnectKey, iTimeout, bIgnoreHint); }

	/* ����: �����Ƽ���PIC (������) */
	/* �Ƽ�����ʹ��PICЧ��̫��,����Ĭ�Ͻ��� */
	/* ���øú�����,�����Ƽ����ຯ�����سɹ���ʹ����Ӧ��PIC end,���Ϊ�Ƿ�/�ƽ���������� */
	/* �ú�������һ�μ��� */
	/* ����: ��  */
	inline void __stdcall SP_Cloud_PICEnable() { __SP_Cloud_PICEnable(); }

	/* ����: ��ȡ���� (ÿ�ε�������)*/
	/* ����: pNotice; ���չ��� */
	/* ����: iError; �Ƽ��������/״̬�� */
	inline int  __stdcall SP_Verify_GetNotice(OUT char* pNotice) { return  __SP_Verify_GetNotice(pNotice); }

	/* ����: ��֤ ��ȡ���°汾��Ϣ (ÿ�ε�������) */
	/* ����: VersionInfo; ���°汾��Ϣ */
	/* ����: iError; �Ƽ��������/״̬��  */
	inline int  __stdcall SP_Verify_GetLastestVersionInfo(OUT st_SPUpdateVersionInfo* VersionInfo) { return  __SP_Verify_GetLastestVersionInfo(VersionInfo); }

	/* ����: ��֤, ��ȡ�����������Ϣ (ÿ�ε�������)*/
	/* ����: pServerOption; ����������Ϣ */
	/* ����: iError; �Ƽ��������/״̬�� */
	inline int  __stdcall SP_Verify_GetServerOption(OUT st_SPServerOption* pServerOption) { return  __SP_Verify_GetServerOption(pServerOption); }

	/* ����: ��֤, ���ڵ�ǰ�����Ļ����� (ÿ�ε�������)*/
	/* ����: iError; �Ƽ��������/״̬�� */
	inline int  __stdcall SP_Verify_DisablePCSign() { return  __SP_Verify_DisablePCSign(); }

	/* ����: ��֤, ���ڵ�ǰ������IP (ÿ�ε�������)*/
	/* ����: iError; �Ƽ��������/״̬�� */
	inline int  __stdcall SP_Verify_DisableIP() { return  __SP_Verify_DisableIP(); }

	/* ����: ��֤, ������/�������� (������)*/
	/* ����: �Ƿ񱻵��� */
	inline bool __stdcall SP_Verify_AntiDebugger() { return  __SP_Verify_AntiDebugger(); }

	/* ����: ��֤����Socks5���� Ĭ�ϲ�����Socks5���� �ɿͻ���ֱ������� */
	/* ����: bEnable; �Ƿ�����Socks5���� ���������Socks5�������Ҫ��ʱ��������false */
	/* ����: szIP; Socks5����IP��ַ/���� */
	/* ����: wPort; Socks5����˿ں� */
	/* ����: szUsername; Socks5�����˺� û������NULL */
	/* ����: szPassword; Socks5�������� û������NULL */
	inline void  __stdcall SP_Verify_SetSocks5(bool bEnable = true, const char* szHost = NULL, int wPort = 0, const char* szUsername = NULL, const char* szPassword = NULL) { return __SP_Verify_SetSocks5(bEnable, szHost, wPort, szUsername, szPassword); }

	/* ����: ��֤, ������֤ʵ�ʷ��ʵ�IP�������������ڶ���·��ע�⣬�������ͻ��˵�������Կ��Ҫ����һ��,Ҳ��Ҫ�ڵ���SP_Verify_Init����ʹ�� */
	/* ����: szHost; IP������ */
	/* ����: wPort; �˿� */
	inline void __stdcall SP_Verify_SetHost(const char* szHost, unsigned short wPort) { return __SP_Verify_SetHost(szHost, wPort); }

	/* ����: ��֤, ��ȡ���ÿ�,�������ر���������Ч (ÿ�ε�������)*/
	/* ����: szCard; �����ȡ�������ÿ� */
	/* ����: iError; �Ƽ��������/״̬�� */
	inline int  __stdcall SP_Verify_GetTrialCard(OUT char szCard[41]) { return __SP_Verify_GetTrialCard(szCard); }

	/* ����: ��֤ ���ܵ�¼ */
	/* ����: szCard; ���� */
	/* ����: �ο��Ƽ���iError  */
	inline int  __stdcall SP_Verify_CardLogin(const char* szCard) { return __SP_Verify_CardLogin(szCard); }

	/* ����: ��֤ �˺������¼ */
	/* ����: szUsername; �˺� */
	/* ����: szPassword; ���� */
	/* ����: �ο��Ƽ���iError  */
	inline int  __stdcall SP_Verify_UserLogin(const char* szUsername, const char* szPassword) { return __SP_Verify_UserLogin(szUsername, szPassword); }

	/* ����: ��֤ ���ܽ�� (ÿ�ε�������)*/
	/* ����: szCard; ���� */
	/* ����: UnBindInfo; ���ս�󷵻���Ϣ(ʣ�������,����ʱ��) */
	/* ����: �ο��Ƽ���iError */
	inline int  __stdcall SP_Verify_CardUnbind(const char* szCard, OUT st_SPUnBindInfo* UnBindInfo) { return __SP_Verify_CardUnbind(szCard, UnBindInfo); }

	/* ����: ��֤ �˺Ž�� (ÿ�ε�������)*/
	/* ����: szCard; ���� */
	/* ����: UnBindInfo; ���ս�󷵻���Ϣ(ʣ�������,����ʱ��) */
	/* ����: �ο��Ƽ���iError */
	inline int  __stdcall SP_Verify_UserUnbind(const char* szUsername, const char* szPassword, OUT st_SPUnBindInfo* UnBindInfo) { return __SP_Verify_UserUnbind(szUsername, szPassword, UnBindInfo); }

	/* ����: ��֤ �˺ų�ֵ */
	/* ����: szUsername; �˺� */
	/* ����: szRechargeCards; ��ֵ�� ������д�����ֵ�����û��з�ƴ�� */
	/* ����: RechargeInfo; ���ճ�ֵ������Ϣ */
	/* ����: �ο��Ƽ���iError  */
	inline int  __stdcall SP_Verify_UserRecharge(const char* szUsername, const char* szRechargeCards, OUT st_SPUserRechargeInfo* RechargeInfo) { return __SP_Verify_UserRecharge(szUsername, szRechargeCards, RechargeInfo); }

	/* ����: ��֤ �˺�ע�� */
	/* ����: szUsername; �˺� */
	/* ����: szPassword; ���� */
	/* ����: szSuperPWD; �������� */
	/* ����: szRechargeCards; ��ֵ�� ������д�����ֵ�����û��з�ƴ�� */
	/* ����: �ο��Ƽ���iError  */
	inline int  __stdcall SP_Verify_UserRegister(const char* szUsername, const char* szPassword, const char* szSuperPWD, const char* szRechargeCards) { return __SP_Verify_UserRegister(szUsername, szPassword, szSuperPWD, szRechargeCards); }

	/* ����: ��֤ �˺Ÿ��� */
	/* ����: szUsername; �˺� */
	/* ����: szSuperPWD; �������� */
	/* ����: szNewPWD; ������ */
	/* ����: �ο��Ƽ���iError  */
	inline int  __stdcall SP_Verify_UserChangePWD(const char* szUsername, const char* szSuperPWD, const char* szNewPWD) { return __SP_Verify_UserChangePWD(szUsername, szSuperPWD, szNewPWD); }

	/* ����: ��֤, ��ȡ���ܵ�ǰ�������߿ͻ�����Ϣ,�迨����Ч (ÿ�ε�������)*/
	/* ���صĿͻ�����Ϣ�ᱣ�浽������Ŀռ� ���´ε���CardQueryOnlineClient��UserQueryOnlineClient����*/
	/* ����: szCard; ���� */
	/* ����: pOnlineInfoHead; ���߿ͻ�����Ϣ */
	/* ����: iError; �Ƽ��������/״̬�� */
	inline int  __stdcall SP_Verify_CardQueryOnlineClient(const char* szCard, OUT st_SPOnlineInfoHead* pOnlineInfoHead) { return __SP_Verify_CardQueryOnlineClient(szCard, pOnlineInfoHead); }

	/* ����: ��֤, ��ȡĳ���˺ŵ�ǰ���߿ͻ�����Ϣ,���˺�������Ч (ÿ�ε�������)*/
	/* ���صĿͻ�����Ϣ�ᱣ�浽������Ŀռ� ���´ε���CardQueryOnlineClient��UserQueryOnlineClient����*/
	/* ����: szUsername; �˺� */
	/* ����: szPassword; ���� */
	/* ����: pOnlineInfoHead; ���߿ͻ�����Ϣ */
	/* ����: iError; �Ƽ��������/״̬�� */
	inline int  __stdcall SP_Verify_UserQueryOnlineClient(const char* szUsername, const char* szPassword, OUT st_SPOnlineInfoHead* pOnlineInfoHead) { return __SP_Verify_UserQueryOnlineClient(szUsername, szPassword, pOnlineInfoHead); }

	/* ����: ��֤, �رտ���������������,�迨����Ч (ÿ�ε�������)*/
	/* ����: szCard; ���� */
	/* ����: iCID; �ͻ���ID ��SP_Verify_CardQueryOnlineClient�����ɻ�� */
	/* ����: iError; �Ƽ��������/״̬�� */
	inline int  __stdcall SP_Verify_CardCloseOnlineByCID(const char* szCard, int iCID) { return __SP_Verify_CardCloseOnlineByCID(szCard, iCID); }

	/* ����: ��֤, �ر��˺�������������,���˺�������Ч (ÿ�ε�������)*/
	/* ����: szUsername; �˺� */
	/* ����: szPassword; ���� */
	/* ����: iCID; �ͻ���ID ��SP_Verify_UserQueryOnlineClient�����ɻ�� */
	/* ����: iError; �Ƽ��������/״̬�� */
	inline int  __stdcall SP_Verify_UserCloseOnlineByCID(const char* szUsername, const char* szPassword, int iCID) { return __SP_Verify_UserCloseOnlineByCID(szUsername, szPassword, iCID); }


	/* ����: ��֤, ��ȡ�����Ѱ󶨵����еĻ�������Ϣ,�迨����Ч (ÿ�ε�������)*/
	/* ���صĻ�������Ϣ�ᱣ�浽������Ŀռ� ���´ε���CardQueryPCSigns��UserQueryPCSigns����*/
	/* ����: szCard; ���� */
	/* ����: pPCSignInfos; �Ѱ󶨻�������Ϣ */
	/* ����: iError; �Ƽ��������/״̬�� */
	inline int  __stdcall SP_Verify_CardQueryPCSignInfos(const char* szCard, OUT st_SPQueryPCSignInfos* pPCSignInfos) { return __SP_Verify_CardQueryPCSignInfos(szCard, pPCSignInfos); }

	/* ����: ��֤, ��ȡ�˺��Ѱ󶨵����еĻ�����Ϣ,���˺�������Ч (ÿ�ε�������)*/
	/* ���صĻ�������Ϣ�ᱣ�浽������Ŀռ� ���´ε���CardQueryPCSigns��UserQueryPCSigns����*/
	/* ����: szUsername; �˺� */
	/* ����: szPassword; ���� */
	/* ����: pPCSignInfos; �Ѱ󶨻�������Ϣ */
	/* ����: iError; �Ƽ��������/״̬�� */
	inline int  __stdcall SP_Verify_UserQueryPCSignInfos(const char* szUsername, const char* szPassword, OUT st_SPQueryPCSignInfos* pPCSignInfos) { return __SP_Verify_UserQueryPCSignInfos(szUsername, szPassword, pPCSignInfos);  }

	/* ����: ��֤, �Ƴ����ܵ�ĳ���Ѱ󶨻�����,�迨����Ч (ÿ�ε�������)*/
	/* ����: szCard; ���� */
	/* ����: szUnbindPCSign; ���Ƴ��Ļ����� ����NULL����0�ַ����ʾΪ���������� */
	/* ����: bUnbindIP; �Ƿ�ͬʱ���IP�İ� */
	/* ����: iError; �Ƽ��������/״̬�� */
	inline int  __stdcall SP_Verify_CardRemovePCSign(const char* szCard, const char* szUnbindPCSign, bool bUnbindIP) { return __SP_Verify_CardRemovePCSign(szCard, szUnbindPCSign, bUnbindIP);  }

	/* ����: ��֤, �ر��˺�������������,���˺�������Ч (ÿ�ε�������)*/
	/* ����: szUsername; �˺� */
	/* ����: szPassword; ���� */
	/* ����: szUnbindPCSign; ���Ƴ��Ļ����� ����NULL����0�ַ����ʾΪ���������� */
	/* ����: bUnbindIP; �Ƿ�ͬʱ���IP�İ� */
	/* ����: iError; �Ƽ��������/״̬�� */
	inline int  __stdcall SP_Verify_UserRemovePCSign(const char* szUsername, const char* szPassword, const char* szUnbindPCSign, bool bUnbindIP) { return __SP_Verify_UserRemovePCSign(szUsername, szPassword, szUnbindPCSign, bUnbindIP);  }

	/* ����: ��֤, ͨ���˺ŵĹ������һ��˺����� (ÿ�ε�������)*/
	/* ����: szCard; ��ֵ���˺���Ŀ��ܻ����˺ŵ����� */
	/* ����: szUsername; �˺ŵ��˺��� */
	/* ����: szPassword; �˺ŵ����� */
	/* ����: szSuperPWD; �˺ŵĳ������� */
	/* ����: iError; �Ƽ��������/״̬�� */
	inline int  __stdcall SP_Verify_UserRetrievePassword(const char* szCard, OUT char szUsername[33], OUT char szPassword[33], OUT char szSuperPWD[33]) { return __SP_Verify_UserRetrievePassword(szCard, szUsername, szPassword, szSuperPWD); }


	/* ����: ��ѯ�Ƿ��ѵ�¼ ԭ���ǵ���һ�������� (ÿ�ε�������)*/
	/* ����: �Ƿ��¼ */
	inline bool  __stdcall SP_Verify_IsLogin() { return __SP_Verify_IsLogin(); }

	/* ����: ��ѯ����ŵ���ϸ��Ϣ (������)*/
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: pMsg; ���մ�����Ϣ */
	inline void  __stdcall SP_Verify_GetErrorMsg(int iError, OUT char* pMsg) { return __SP_Verify_GetErrorMsg(iError, pMsg); }

	/* ����: �Ƽ������� (ÿ�ε�������) */
	/* ����: dwCloudID; �Ƽ���ID; (�������0) */
	/* ����: pData; �Ƽ������ݰ�ָ�� */
	/* ����: dwLength; �Ƽ������ݰ����� */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƽ����Ƿ�ɹ�; �������, �ɲο�iError */
	inline bool  __stdcall SP_CloudComputing(int dwCloudID, unsigned char* pData, int dwLength, int* iError) { return __SP_CloudComputing(dwCloudID, pData, dwLength, iError); }

	/* ����: �Ƽ������� (�����޼���) */
	/* ����: dwCloudID; �Ƽ���ID */
	/* ����: pData; �Ƽ������ݰ�ָ�� */
	/* ����: dwLength; �Ƽ������ݰ����� */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƽ����Ƿ�ɹ�; �������, �ɲο�iError */
	inline bool  __stdcall SP_CloudComputing_NoEncrypt(int dwCloudID, unsigned char* pData, int dwLength, int* iError) { return __SP_CloudComputing_NoEncrypt(dwCloudID, pData, dwLength, iError); }

	/* ����: �Ƽ��㷵������ */
	/* ����: dwYunID; �Ƽ���ID; (�������0) */
	/* ����: pData; ��Ŷ�ȡ���ݵĻ����� */
	/* ����: dwLength; Ҫ��ȡ�ĳ��� */
	/* ����: ��ȡ���ĳ��� */
	inline int  __stdcall SP_CloudResult(int dwCloudID, unsigned char* pData, int dwLength) { return __SP_CloudResult(dwCloudID, pData, dwLength); }

	/* ����: �Ƽ���, ���������ܳ��� */
	/* ����: dwCloudID; �Ƽ���ID; (�������0) */
	/* ����: �ܳ��� */
	inline int  __stdcall SP_CloudResultLength(int dwCloudID) { return __SP_CloudResultLength(dwCloudID); }

	/* ����: �Ƽ���, ��������ʣ��δ������ */
	/* ����: dwCloudID; �Ƽ���ID; (�������0) */
	/* ����: ��ȡ���ĳ��� */
	inline int  __stdcall SP_CloudResultRest(int dwCloudID) { return __SP_CloudResultRest(dwCloudID); }

	/* ����: �Ƽ���, Ƶ����֤ (���鴴��һ���߳���Ƶ������, ����30�����һ��) */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ���֤�ɹ�; �������, �ɲο�iError  */
	inline bool  __stdcall SP_Cloud_Beat(int* iError) { return __SP_Cloud_Beat(iError); }

	/* ����: �Ƽ���, ��ȡ��ǰ��½�������������� (ÿ�ε�������) */
	/* ����: szAgent[44] */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	inline bool  __stdcall SP_Cloud_GetCardAgent(char szAgent[44], int* iError) { return __SP_Cloud_GetCardAgent(szAgent, iError); }

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵĿ����� (ÿ�ε�������) */
	/* ����: szCardType[36] */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	inline bool  __stdcall SP_Cloud_GetCardType(char szCardType[36], int* iError) { return __SP_Cloud_GetCardType(szCardType, iError); }

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵ�¼ʱ��¼��IP��ַ (ÿ�ε�������) */
	/* ����: szIPAddress[44] */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	inline bool  __stdcall SP_Cloud_GetIPAddress(char szIPAddress[44], int* iError) { return __SP_Cloud_GetIPAddress(szIPAddress, iError); }

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵı�ע (ÿ�ε�������) */
	/* ����: szRemarks[132] */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	inline bool  __stdcall SP_Cloud_GetRemarks(char szRemarks[132], int* iError) { return __SP_Cloud_GetRemarks(szRemarks, iError); }

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵĴ���ʱ��� (ÿ�ε�������) */
	/* ����: iCreatedTimeStamp */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	inline bool  __stdcall SP_Cloud_GetCreatedTimeStamp(__int64* iCreatedTimeStamp, int* iError) { return __SP_Cloud_GetCreatedTimeStamp(iCreatedTimeStamp, iError); }

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵļ���ʱ��� (ÿ�ε�������) */
	/* ����: iActivatedTimeStamp */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	inline bool  __stdcall SP_Cloud_GetActivatedTimeStamp(__int64* iActivatedTimeStamp, int* iError) { return __SP_Cloud_GetActivatedTimeStamp(iActivatedTimeStamp, iError); }

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵĹ���ʱ��� (ÿ�ε�������) */
	/* ����: iExpiredTimeStamp */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	inline bool  __stdcall SP_Cloud_GetExpiredTimeStamp(__int64* iExpiredTimeStamp, int* iError) { return __SP_Cloud_GetExpiredTimeStamp(iExpiredTimeStamp, iError); }

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵ�����¼ʱ��� (ÿ�ε�������) */
	/* ����: iLastLoginTimeStamp */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	inline bool  __stdcall SP_Cloud_GetLastLoginTimeStamp(__int64* iLastLoginTimeStamp, int* iError) { return __SP_Cloud_GetLastLoginTimeStamp(iLastLoginTimeStamp, iError); }

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵ�ʣ����� (ÿ�ε�������) */
	/* ����: iFYI */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	inline bool  __stdcall SP_Cloud_GetFYI(__int64* iFYI, int* iError) { return __SP_Cloud_GetFYI(iFYI, iError); }

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵĶ࿪��������ֵ (ÿ�ε�������) */
	/* ����: iNum */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	inline bool  __stdcall SP_Cloud_GetOpenMaxNum(int* iNum, int* iError) { return __SP_Cloud_GetOpenMaxNum(iNum, iError); }

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵİ󶨻�������ֵ (ÿ�ε�������) */
	/* ����: iBind; �Ƿ���, 1/0 */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	inline bool  __stdcall SP_Cloud_GetBind(int* iBind, int* iError) { return __SP_Cloud_GetBind(iBind, iError); }

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵĻ������� (ÿ�ε�������) */
	/* ����: iBindTime; (��) */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	inline bool  __stdcall SP_Cloud_GetBindTime(__int64* iBindTime, int* iError) { return __SP_Cloud_GetBindTime(iBindTime, iError); }

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵĽ��۳�����ֵ (ÿ�ε�������) */
	/* ����: iDeductSec; (��) */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	inline bool  __stdcall SP_Cloud_GetUnBindDeductTime(__int64* iDeductSec, int* iError) { return __SP_Cloud_GetUnBindDeductTime(iDeductSec, iError); }

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵ�������������ֵ (ÿ�ε�������) */
	/* ����: iNum */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	inline bool  __stdcall SP_Cloud_GetUnBindMaxNum(int* iNum, int* iError) { return __SP_Cloud_GetUnBindMaxNum(iNum, iError); }

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵ��ۼƽ����� (ÿ�ε�������) */
	/* ����: iCountTotal */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	inline bool  __stdcall SP_Cloud_GetUnBindCountTotal(int* iCountTotal, int* iError) { return __SP_Cloud_GetUnBindCountTotal(iCountTotal, iError); }

	/* ����: �Ƽ���, ��ȡ��ǰ��½���ܵ��ۼƽ��۳���ʱ�� (ÿ�ε�������) */
	/* ����: iDeductTimeTotal; (��) */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	inline bool  __stdcall SP_Cloud_GetUnBindDeductTimeTotal(__int64* iDeductTimeTotal, int* iError) { return __SP_Cloud_GetUnBindDeductTimeTotal(iDeductTimeTotal, iError); }

	/* ����: �Ƽ���, �Ƴ���ǰ�Ƽ��������֤��Ϣ (ÿ�ε�������) */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �����Ƿ�ɹ�; �������, �ɲο�iError  */
	/* ���øú����� ������޷�ͨ��PICУ�� �������û������������� */
	inline bool  __stdcall SP_Cloud_Offline(int* iError) { return __SP_Cloud_Offline(iError); }

	/* ����: �Ƽ���, ��ȡ��ǰ��½�Ŀ��� (��¼�ɹ������) */
	/* ����: szCard */
	inline void  __stdcall SP_Cloud_GetCard(char szCard[41]) { __SP_Cloud_GetCard(szCard); }

	/* ����: �Ƽ���, ��ȡ��ǰ��½���˺� (��¼�ɹ������) */
	/* ����: szUser */
	inline void  __stdcall SP_Cloud_GetUser(OUT char szUser[33]) { __SP_Cloud_GetUser(szUser); }

	/* ����: �Ƽ���, ���õ�ǰ��½�Ŀ��� (��¼�ɹ������) */
	/* ����: iError; �Ƽ��������/״̬�� */
	inline void  __stdcall SP_Cloud_DisableCard(OUT OPTIONAL int* iError) { __SP_Cloud_DisableCard(iError); }

	/* ����: �Ƽ���, ��ȡ��ǰ�ͻ���ID (��¼�ɹ������) */
	inline int  __stdcall SP_Cloud_GetCID() { return __SP_Cloud_GetCID(); }

	/* ����: �Ƽ���, ��ȡ��ǰ�������߿ͻ������� (S��¼�ɹ������) */
	/* ����: iError; �Ƽ��������/״̬�� */
	inline bool  __stdcall SP_Cloud_GetOnlineCount(int* iCount, int* iError) { return __SP_Cloud_GetOnlineCount(iCount, iError); }

	/* ����: �Ƽ���, ��ȡ��ǰ�豸������ (ÿ�ε�������) */
	/* ����: szPCSign ��������� */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	inline bool  __stdcall SP_Cloud_GetPCSign(char szPCSign[33], OUT OPTIONAL int* iError) { return __SP_Cloud_GetPCSign(szPCSign, iError); }
	
	/* ����: �Ƽ���, �۳���ǰ���ܵ���, �����û�ʹ����ĳЩ���⹦����Ҫ����۷ѵĳ��� (ÿ�ε�������) */
	/* ������iFYICount����Ҫ�۳��ĵ������� */
	/* ������iSurplusFYI���۳���ʣ�µĵ�������δ�ܿ۳��ɹ����ֵ��ʾΪ��ǰ���ܵĵ��� */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError */
	inline bool  __stdcall SP_Cloud_DeductFYI(__int64 iFYICount, OUT __int64* iSurplusFYI, OUT OPTIONAL int* iError) { return __SP_Cloud_DeductFYI(iFYICount, iSurplusFYI, iError); }

	/* ����: �Ƽ���, �����Ƽ������ϵͳ�汾��ʶ (��¼֮ǰʹ��) */
	/* ����: szWinVer; �Զ������ϵͳ�汾��ʶ, ���Ϊ��, ��Ϊ�����߼���ȡ����ϵͳ�汾 */
	inline bool  __stdcall SP_Cloud_SetWinVer(char* szWinVer) { return __SP_Cloud_SetWinVer(szWinVer); }

	/* ����: �Ƽ���, ��ȡ��ǰ��½���������ڵĽ����� (ÿ�ε�������) */
	/* ����: iCount */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	inline bool  __stdcall SP_Cloud_GetUnBindCount(int* iCount, OUT OPTIONAL int* iError) { return __SP_Cloud_GetUnBindCount(iCount, iError); }

	/* ����: �Ƽ���, ��ȡƵ����֤���������� (ÿ�ε�������, �ù�����Ҫ�ڷ���� [�����������] ����) */
	/* ����: iTotalCount; ���������� */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	inline bool  __stdcall SP_Cloud_GetOnlineTotalCount(unsigned int* iTotalCount, OUT OPTIONAL int* iError) { return __SP_Cloud_GetOnlineTotalCount(iTotalCount, iError); }

	/* ����: �Ƽ���, ��ȡ���߿������� (ÿ�ε�������, �ù�����Ҫ�ڷ���� [�����������] ����) */
	/* ����: iTotalCount; ���������� */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	/* ˵��: */
	/*		������100�Ŷ࿪����Ϊ10�Ŀ���, ���п��ܶ��ѵ�¼ռ���࿪���� */
	/*		��ʱ�����ӵ��100*10=1000���������� */
	/*		1000������������ʵ��������100�����߿���, ���õ�ǰ�ӿں�, iTotalCountֵ��Ϊ100 */
	inline bool  __stdcall SP_Cloud_GetOnlineCardsCount(unsigned int* iTotalCount, OUT OPTIONAL int* iError) { return __SP_Cloud_GetOnlineCardsCount(iTotalCount, iError); }

	/* ����: �Ƽ���, ��ȡָ������������������ (ÿ�ε�������) */
	/* ����: szCard; ����; ��дNULLΪ��ǰ��¼�Ƽ���Ŀ��� */
	/* ����: iTotalCount; ���������� */
	/* ����: iError; �Ƽ��������/״̬�� */
	/* ����: �Ƿ��ȡ�ɹ�; �������, �ɲο�iError  */
	/* ˵��: */
	/*		������1�Ŷ࿪����Ϊ10�Ŀ��� */
	/*		��ʱ�û�ʹ�����ſ��ܵ�¼��3���ͻ���, ���õ�ǰ�ӿں�, iTotalCountֵ��Ϊ3 */
	inline bool  __stdcall SP_Cloud_GetOnlineCountByCard(OPTIONAL const char* szCard, unsigned int* iTotalCount, OUT OPTIONAL int* iError) { return __SP_Cloud_GetOnlineCountByCard(szCard, iTotalCount, iError); }
#endif // __USE_FUNC_PERFIX == 0
}

#endif // !_SP_VERIFY_HEADER