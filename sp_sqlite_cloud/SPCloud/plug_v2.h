#pragma once

/* ����: ������Ϣ */
struct tagCardInfo {
	char*	szWhom;//		�ƿ���;
	char*	szType;//		��������;
	char*	szIPAddress;//	IP��ַ;
	char*	szRemarks;//	���ܱ�ע;
	__int64	nCreateTimeStamp;//	���ܴ���ʱ���;
	__int64	nActivatedTimeStamp;//���ܼ���ʱ���;
	__int64	nExpiredTimeStamp;//	���ܹ���ʱ���;
	__int64 nLastLoginTimeStamp;//	��������¼ʱ���;
	__int64	nFYI;//				����;
	int		nOpenMaxNum;//		�࿪����;
	int		bBind;//				�Ƿ�󶨻���(0/1);
	__int64	nBindTime;//			���ʱ��(��);
	__int64	nUnBindDeductTime;//	���۳�(��);
	int		nUnBindMaxNum;//		��������;
	int		nUnBindCountTotal;//		�ۼƽ����ܴ���;
	__int64 nUnBindDeductTimeTotal;//	�ۼƽ����ܿ۳�(��);
	char*	szPCSign;//			������
	char*	szSoftwareOwn;//	�����������λ
	int		nUnBindCount;//					������ �Ѿ����Ĵ���;
	int		nFreeUnBindCount;//		����;	������ ��ѽ��Ĵ���;
	int		Reserved[4];//		�����ֶ�;
};

/* ����: ���ÿ��ܵ��� */
typedef bool(__stdcall* pfn_SP_Cloud_SetCardFYI_v2)(char* szSoftwareName, char* szCard, __int64 nNewFYI);

/* ����: ���ÿ��ܵ��� */
typedef bool(__stdcall* pfn_SP_Cloud_AddCardFYI_v2)(char* szSoftwareName, char* szCard, __int64 nFYI);

/* ����: ���ÿ��� */
typedef bool(__stdcall* pfn_SP_Cloud_EnableCard_v2)(char* szSoftwareName, char* szCard);

/* ����: ���ÿ��� */
typedef bool(__stdcall* pfn_SP_Cloud_DisableCard_v2)(char* szSoftwareName, char* szCard, char* szCause);

/* ����: ���ÿ��ܱ�ע */
typedef bool(__stdcall* pfn_SP_Cloud_SetCardRemarks_v2)(char* szSoftwareName, char* szCard, char* szRemarks);

/* ����: ���ÿ��ܵ���ʱ�� */
typedef bool(__stdcall* pfn_SP_Cloud_SetExpiredTime_v2)(char* szSoftwareName, char* szCard, __int64 ExpiredTime);

/* ����: ��⿨�� (�黹���ʱ��) */
typedef bool(__stdcall* pfn_SP_Cloud_EnableCardGiveback_v2)(char* szSoftwareName, char* szCard);

/* ����: �������ܵ���ʱ�� */
typedef bool(__stdcall* pfn_SP_Cloud_AddExpiredTime_v2)(char* szSoftwareName, char* szCard, __int64 ExpiredTime);

/* ����: ����� */
typedef bool(__stdcall* pfn_SP_Cloud_UnBindCard_v2)(char* szSoftwareName, char* szCard);

/* ����: �����ֵ */
typedef bool(__stdcall* pfn_SP_Cloud_AgentRecharge_v2)(char* szSoftwareName, char* szAgent, double Balance, int iHour);

/* ����: �����˺Ŵ������� (һ��һ��) */
typedef bool(__stdcall* pfn_SP_Cloud_CreateCard_v2)(char* szSoftwareName, char* szCardType, int iHour, char* szRemarks, IN OUT char* szCard);

/* ����: �ߵ������û�, (��Ӧ�ͻ��˴�����-15) */
typedef bool(__stdcall* pfn_SP_Cloud_Disconnect_v2)(char* szSoftwareName, char* szCard);

/* ����: ���÷������ݰ����ߴ� */
typedef void(__stdcall* pfn_SP_Cloud_SetBufferSizeMax_v2)(unsigned int iSize);

/* ����: ��ָ�������˺ſ���; ���ؼ�ʱ��ο�������Ϣ (�������ʧ��, �����ᱻ�۷�, ����Ҳ��������) */
/* ����: szCard; �����ɵ����������㹻��Ļ�������������ɵĿ���, �������ܳ������Ϊ41(������β\0), ������Ҫ����10�ſ���, ����������ߴ�Ӧ��Ϊ10*41; */
/* ����: szError; ������Ϣ */
typedef bool(__stdcall* pfn_SP_Cloud_CreateCardEx_v2)(char* szSoftwareName, char* szAgent, char* szPassword, char* szCardType, int iHour, int iNum, char* szRemarks, IN OUT char* szCard, IN OUT char szError[128]);

/* ����: �����˺Ŵ����Զ��忨�ܣ�����bool���ж��Ƿ����ɳɹ� */
/* ����: szZDYCard; ���������û��з��ָ�; */
typedef bool(__stdcall* pfn_SP_Cloud_ZDYCreateCard_v2)(char* szSoftwareName, char* szCardType, int iHour, char* szRemarks, IN char* szZDYCard);

/* ����: �˺ų�ֵ */
typedef bool(__stdcall* pfn_SP_Cloud_UserRecharge_v2)(char* szSoftwareName, char* szUser, char* szCard, IN OUT char szError[128]);

/* ����: ����IP(���������) */
/* ����: szCause; ԭ�� */
typedef bool(__stdcall* pfn_SP_Cloud_DisableIP_v2)(char* szIP, char szCause[256]);

/* ����: ����IP(�Ƴ�������) */
typedef bool(__stdcall* pfn_SP_Cloud_EnableIP_v2)(char* szIP);

/* ����: ���û�����(���������) */
/* ����: szCause; ԭ�� */
typedef bool(__stdcall* pfn_SP_Cloud_DisablePCSign_v2)(char* szSoftwareName, char* szPCSign, char szCause[256]);

/* ����: ���û�����(�Ƴ�������) */
typedef bool(__stdcall* pfn_SP_Cloud_EnablePCSign_v2)(char* szSoftwareName, char* szPCSign);


/* ����: ���ܽӿ� v2 */
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
