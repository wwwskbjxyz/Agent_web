#ifndef NOMINMAX
#define NOMINMAX
#endif
#include <windows.h>
#include <stdint.h>
#include <atlstr.h>
#include <vector>
#include <string>
#include <algorithm>
#include <exception>
#include <mutex>
#include <cstdio>
#include <cstring>
#include "plug_v2.h"
#include "common/cloud_crypto.h"
#include "common/cloud_protocol.h"
#include "sqlite3.h"

#pragma comment(linker, "/EXPORT:SP_CloudComputing_Init_v2=_SP_CloudComputing_Init_v2@4")
#pragma comment(linker, "/EXPORT:SP_CloudComputing_Callback_v2=_SP_CloudComputing_Callback_v2@32")
#pragma comment(linker, "/EXPORT:SP_Notify_Callback_CreatedCard_v2=_SP_Notify_Callback_CreatedCard_v2@32")
#pragma comment(linker, "/EXPORT:SP_Notify_Callback_EnabledCard_v2=_SP_Notify_Callback_EnabledCard_v2@20")
#pragma comment(linker, "/EXPORT:SP_Notify_Callback_DisabledCard_v2=_SP_Notify_Callback_DisabledCard_v2@20")
#pragma comment(linker, "/EXPORT:SP_Notify_Callback_PreUnInit_v2=_SP_Notify_Callback_PreUnInit_v2@0")

namespace
{
    enum class CloudCommand : DWORD
    {
        Test = 1,
        RemoteQuery = 2,
    };

    CRITICAL_SECTION g_Lock;
    bool g_LockInitialized = false;
    INIT_ONCE g_LockInitOnce = INIT_ONCE_STATIC_INIT;
    std::once_flag g_console_once;

    DWORD g_ResultBufferMax = 0;

    SYSTEMTIME TimeStamp2SystemTime(__int64 ts)
    {
        __int64 tmpTs = (ts + 8 * 60 * 60) * 10000000i64 + 116444736000000000i64;
        FILETIME ft;
        SYSTEMTIME st;
        ft.dwLowDateTime = (DWORD)tmpTs;
        ft.dwHighDateTime = tmpTs >> 32;
        FileTimeToSystemTime(&ft, &st);
        return st;
    }

    void ensure_console()
    {
        std::call_once(g_console_once, []() {
            BOOL attached = AttachConsole(ATTACH_PARENT_PROCESS);
            if (!attached)
            {
                attached = AllocConsole();
            }

            if (!attached)
            {
                return;
            }

            FILE* out_stream = nullptr;
            FILE* err_stream = nullptr;
            FILE* in_stream = nullptr;
#if defined(_WIN32)
            freopen_s(&out_stream, "CONOUT$", "w", stdout);
            freopen_s(&err_stream, "CONOUT$", "w", stderr);
            freopen_s(&in_stream, "CONIN$", "r", stdin);
#else
            out_stream = freopen("/dev/tty", "w", stdout);
            err_stream = freopen("/dev/tty", "w", stderr);
            in_stream = freopen("/dev/tty", "r", stdin);
#endif
            (void)out_stream;
            (void)err_stream;
            (void)in_stream;
            SetConsoleCP(CP_UTF8);
            SetConsoleOutputCP(CP_UTF8);
            printf("[Server] Console initialized for cloud plugin\n");
            fflush(stdout);
        });
    }

    void log_structured_exception(const char* context, DWORD code)
    {
        LPSTR buffer = nullptr;
        DWORD flags = FORMAT_MESSAGE_ALLOCATE_BUFFER | FORMAT_MESSAGE_FROM_SYSTEM | FORMAT_MESSAGE_IGNORE_INSERTS;
        DWORD length = FormatMessageA(flags, nullptr, code, 0, reinterpret_cast<LPSTR>(&buffer), 0, nullptr);
        if (length == 0 || !buffer)
        {
            DWORD format_error = GetLastError();
            printf("[Server] %s - Structured exception 0x%08lX (FormatMessage failed: %lu)\n", context, code, format_error);
        }
        else
        {
            while (length > 0 && (buffer[length - 1] == '\r' || buffer[length - 1] == '\n'))
            {
                buffer[--length] = '\0';
            }
            printf("[Server] %s - Structured exception 0x%08lX: %s\n", context, code, buffer);
            LocalFree(buffer);
        }
        fflush(stdout);
    }

    BOOL CALLBACK initialize_global_lock(PINIT_ONCE, PVOID, PVOID*)
    {
        InitializeCriticalSection(&g_Lock);
        g_LockInitialized = true;
        return TRUE;
    }

    void ensure_global_lock()
    {
        ensure_console();
        InitOnceExecuteOnce(&g_LockInitOnce, initialize_global_lock, nullptr, nullptr);
    }

    // 构造统一的错误响应
    std::vector<std::uint8_t> make_error_response(int rc, const std::string& message)
    {
        std::vector<std::uint8_t> plain;
        sp::BinaryWriter writer(plain);
        writer.write_uint32(static_cast<std::uint32_t>(rc));
        writer.write_string(message);
        return plain;
    }

    std::string escape_sql_literal(const std::string& value)
    {
        std::string escaped;
        escaped.reserve(value.size());
        for (char ch : value)
        {
            if (ch == '\'')
            {
                escaped.push_back('\'');
            }
            escaped.push_back(ch);
        }
        return escaped;
    }

    std::vector<std::uint8_t> make_sql_response(bool expects_result_set, const std::string& sql)
    {
        std::vector<std::uint8_t> plain;
        sp::BinaryWriter writer(plain);
        writer.write_uint32(SQLITE_OK);
        writer.write_uint32(expects_result_set ? 1u : 0u);
        writer.write_string(sql);
        return plain;
    }

    // 处理获取全部代理信息
    std::vector<std::uint8_t> handle_agent_get_all(const std::string& db_path)
    {
        printf("[Server] AgentGetAll request, database=%s\n", db_path.c_str());
        fflush(stdout);
        std::string sql =
            "SELECT User, Password, AccountBalance, AccountTime, Duration, Authority, "
            "CardTypeAuthName, CardsEnable, Remarks, FNode, Stat, deltm, Duration_, "
            "Parities, TatalParities FROM Agents ORDER BY User ASC;";
        return make_sql_response(true, sql);
    }

    // 处理按用户名查询代理
    std::vector<std::uint8_t> handle_agent_get_by_username(const std::string& db_path, const std::string& username)
    {
        printf("[Server] AgentGetByUsername request, database=%s, user=%s\n", db_path.c_str(), username.c_str());
        fflush(stdout);
        std::string escaped_username = escape_sql_literal(username);
        std::string sql =
            "SELECT User, Password, AccountBalance, AccountTime, Duration, Authority, "
            "CardTypeAuthName, CardsEnable, Remarks, FNode, Stat, deltm, Duration_, "
            "Parities, TatalParities FROM Agents WHERE lower(User) = lower('" + escaped_username + "') LIMIT 1;";
        return make_sql_response(true, sql);
    }

    // 批量更新代理状态
    std::vector<std::uint8_t> handle_agent_set_status(const std::string& db_path, int enable, const std::vector<std::string>& names)
    {
        printf("[Server] AgentSetStatus request, database=%s, enable=%d, count=%zu\n", db_path.c_str(), enable, names.size());
        fflush(stdout);
        if (names.empty())
        {
            return make_sql_response(false, "");
        }

        int stat_value = enable ? 0 : 1;
        int cards_enable = enable ? 1 : 0;

        std::string sql = "UPDATE Agents SET Stat = " + std::to_string(stat_value) +
            ", CardsEnable = " + std::to_string(cards_enable) + " WHERE lower(User) IN (";

        for (std::size_t i = 0; i < names.size(); ++i)
        {
            if (i != 0)
            {
                sql += ", ";
            }
            sql += "lower('" + escape_sql_literal(names[i]) + "')";
        }
        sql += ");";

        return make_sql_response(false, sql);
    }

    // 更新代理备注
    std::vector<std::uint8_t> handle_agent_update_remark(const std::string& db_path, const std::string& username, const std::string& remark)
    {
        printf("[Server] AgentUpdateRemark request, database=%s, user=%s\n", db_path.c_str(), username.c_str());
        fflush(stdout);
        std::string escaped_username = escape_sql_literal(username);
        std::string escaped_remark = escape_sql_literal(remark);
        std::string sql =
            "UPDATE Agents SET Remarks = '" + escaped_remark + "' WHERE lower(User) = lower('" + escaped_username + "');";
        return make_sql_response(false, sql);
    }

    // 指令分发
    std::vector<std::uint8_t> dispatch_query(sp::CloudQuery query, sp::BinaryReader& payload_reader)
    {
        switch (query)
        {
        case sp::CloudQuery::AgentGetAll:
        {
            std::string db_path = payload_reader.read_string();
            return handle_agent_get_all(db_path);
        }
        case sp::CloudQuery::AgentGetByUsername:
        {
            std::string db_path = payload_reader.read_string();
            std::string username = payload_reader.read_string();
            return handle_agent_get_by_username(db_path, username);
        }
        case sp::CloudQuery::AgentSetStatus:
        {
            std::string db_path = payload_reader.read_string();
            int enable = payload_reader.read_int32();
            int count = payload_reader.read_int32();
            std::vector<std::string> names;
            names.reserve(static_cast<std::size_t>(std::max(count, 0)));
            for (int i = 0; i < count; ++i)
            {
                names.push_back(payload_reader.read_string());
            }
            return handle_agent_set_status(db_path, enable, names);
        }
        case sp::CloudQuery::AgentUpdateRemark:
        {
            std::string db_path = payload_reader.read_string();
            std::string username = payload_reader.read_string();
            std::string remark = payload_reader.read_string();
            return handle_agent_update_remark(db_path, username, remark);
        }
        default:
            return make_error_response(SQLITE_ERROR, "Unknown cloud query");
        }
    }

    // 解密 -> 处理 -> 加密
    std::vector<std::uint8_t> process_remote_request(const std::string& card, const unsigned char* data, DWORD length)
    {
        std::vector<std::uint8_t> packet(data, data + length);
        auto plain_request = sp::crypto::decrypt_packet(card, packet);
        sp::BinaryReader reader(plain_request.data(), plain_request.data() + plain_request.size());
        std::uint32_t version = reader.read_uint32();
        if (version != sp::kCloudProtocolVersion)
        {
            printf("[Server] Protocol version mismatch: got %u expected %u\n", version, sp::kCloudProtocolVersion);
            fflush(stdout);
            return sp::crypto::encrypt_packet(card, make_error_response(SQLITE_ERROR, "Protocol version mismatch"));
        }

        sp::CloudQuery query = static_cast<sp::CloudQuery>(reader.read_uint32());
        printf("[Server] Processing query %u\n", static_cast<unsigned>(query));
        fflush(stdout);
        std::vector<std::uint8_t> payload = reader.read_bytes();
        sp::BinaryReader payload_reader(payload.data(), payload.data() + payload.size());
        std::vector<std::uint8_t> plain_response = dispatch_query(query, payload_reader);
        return sp::crypto::encrypt_packet(card, plain_response);
    }
}

tagInterface_v2 g_Interface = { 0 };

// ========================= 核心回调逻辑（仅 C++ 异常） =========================
static int Handle_CloudComputing_Callback_Core(
    const char* szSoftwareName,
    const char* szCard,
    tagCardInfo* pCardInfo,
    DWORD dwCloudID,
    UCHAR* pData,
    DWORD dwLength,
    PVOID pResultBuffer,
    DWORD* dwResultLength)
{
    (void)szSoftwareName;
    (void)pCardInfo;

    const char* card = szCard ? szCard : "";
    printf("[Server] Incoming request from card %s\n", card);
    printf("[Server] CloudID: %lu, payload length: %lu\n",
        static_cast<unsigned long>(dwCloudID),
        static_cast<unsigned long>(dwLength));
    fflush(stdout);

    DWORD result_capacity = dwResultLength ? *dwResultLength : 0;
    if (result_capacity == 0 && g_ResultBufferMax > 0)
    {
        result_capacity = g_ResultBufferMax;
    }

    int result = 0;

    try
    {
        switch (static_cast<CloudCommand>(dwCloudID))
        {
        case CloudCommand::Test:
        {
            CStringA szMsg;
            if (pData && dwLength > 0)
            {
                szMsg.Append(reinterpret_cast<char*>(pData), dwLength);
            }
            szMsg += "C++版本回显信息";
            DWORD text_length = static_cast<DWORD>(szMsg.GetLength());
            if (pResultBuffer && dwResultLength && result_capacity >= text_length)
            {
                std::memcpy(pResultBuffer, szMsg.GetString(), text_length);
                *dwResultLength = text_length;
            }
            else if (dwResultLength)
            {
                *dwResultLength = 0;
                result = -1;
                printf("[Server] Test command response buffer too small (need %lu, have %lu)\n",
                    static_cast<unsigned long>(text_length),
                    static_cast<unsigned long>(result_capacity));
            }
            break;
        }

        case CloudCommand::RemoteQuery:
        {
            if (!dwResultLength)
            {
                printf("[Server] Remote query missing length pointer\n");
                fflush(stdout);
                result = -1;
                break;
            }

            std::string card_string = card;

            try
            {
                auto encrypted_response = process_remote_request(card_string, pData, dwLength);
                if (encrypted_response.empty())
                {
                    printf("[Server] Remote query produced empty response\n");
                    fflush(stdout);
                }

                DWORD required = static_cast<DWORD>(encrypted_response.size());
                *dwResultLength = required;

                if (!pResultBuffer)
                {
                    printf("[Server] Remote query has no result buffer to receive %lu bytes\n",
                        static_cast<unsigned long>(required));
                    fflush(stdout);
                    result = -1;
                    break;
                }

                if (required > result_capacity)
                {
                    printf("[Server] Result buffer too small (need %lu, have %lu)\n",
                        static_cast<unsigned long>(required),
                        static_cast<unsigned long>(result_capacity));
                    fflush(stdout);
                    result = -1;
                    break;
                }

                std::memcpy(pResultBuffer, encrypted_response.data(), encrypted_response.size());
            }
            catch (const std::exception& ex)
            {
                printf("[Server] Remote query error: %s\n", ex.what());
                auto plain = make_error_response(SQLITE_ERROR, std::string("Server exception: ") + ex.what());
                auto encrypted = sp::crypto::encrypt_packet(card_string, plain);
                DWORD required = static_cast<DWORD>(encrypted.size());
                *dwResultLength = required;
                if (pResultBuffer && result_capacity >= required)
                {
                    std::memcpy(pResultBuffer, encrypted.data(), encrypted.size());
                }
                else
                {
                    printf("[Server] Unable to write encrypted error response (need %lu, have %lu)\n",
                        static_cast<unsigned long>(required),
                        static_cast<unsigned long>(result_capacity));
                }
                fflush(stdout);
                result = -1;
            }
            break;
        }

        default:
            printf("[Server] Unknown cloud command: %lu\n", static_cast<unsigned long>(dwCloudID));
            if (dwResultLength)
                *dwResultLength = 0;
            result = -1;
            break;
        }
    }
    catch (const std::exception& ex)
    {
        printf("[Server] Exception: %s\n", ex.what());
        if (dwResultLength)
        {
            std::string card_string = card;
            auto plain = make_error_response(SQLITE_ERROR, std::string("Server exception: ") + ex.what());
            auto encrypted = sp::crypto::encrypt_packet(card_string, plain);
            DWORD required = static_cast<DWORD>(encrypted.size());
            *dwResultLength = required;

            if (pResultBuffer && result_capacity >= required)
            {
                std::memcpy(pResultBuffer, encrypted.data(), encrypted.size());
            }
            else if (pResultBuffer)
            {
                printf("[Server] Unable to write encrypted error response (need %lu, have %lu)\n",
                    static_cast<unsigned long>(required),
                    static_cast<unsigned long>(result_capacity));
            }
        }
        result = -1;
    }

    fflush(stdout);
    return result;
}

// ========================= 外层回调（仅 SEH + 锁，避免 C++ 对象） =========================
extern "C"
{
    int __stdcall SP_CloudComputing_Callback_v2(
        char* szSoftwareName,
        char* szCard,
        tagCardInfo* pCardInfo,
        DWORD dwCloudID,
        UCHAR* pData,
        DWORD dwLength,
        OPTIONAL PVOID pResultBuffer,
        OPTIONAL DWORD* dwResultLength)
    {
        ensure_console();
        ensure_global_lock();


        // 外层只做：进锁 + SEH 保护 + 出锁；不声明任何会析构的 C++ 对象
        int result = 0;

        EnterCriticalSection(&g_Lock);
        __try
        {
            result = Handle_CloudComputing_Callback_Core(
                szSoftwareName,
                szCard,
                pCardInfo,
                dwCloudID,
                pData,
                dwLength,
                pResultBuffer,
                dwResultLength);
        }
        __except (EXCEPTION_EXECUTE_HANDLER)
        {
            DWORD code = GetExceptionCode();
            log_structured_exception("SP_CloudComputing_Callback_v2", code);
            result = -1;
        }
        LeaveCriticalSection(&g_Lock);

        return result;
    }

    int __stdcall SP_Notify_Callback_CreatedCard_v2(
        char* szSoftwareName,
        char* szOperator,
        char* pCard[],
        int iCardCount,
        char* szCardType,
        __int64 iCardTime,
        char* szRemarks)
    {
        ensure_global_lock();
        EnterCriticalSection(&g_Lock);
        CStringA szMsg;
        szMsg.Format("[Server] CreateCard notify: type=%s time=%lld remarks=%s\n", szCardType, iCardTime, szRemarks);
        for (int i = 0; i < iCardCount; ++i)
        {
            szMsg += pCard[i];
            szMsg += "\n";
        }
        printf("%s", szMsg.GetString());
        fflush(stdout);
        LeaveCriticalSection(&g_Lock);
        return 0;
    }

    int __stdcall SP_Notify_Callback_EnabledCard_v2(
        char* szSoftwareName,
        char* szOperator,
        char* pCard[],
        int iCardCount,
        bool bGivebackBanTime)
    {
        ensure_global_lock();
        EnterCriticalSection(&g_Lock);
        CStringA szMsg;
        szMsg.Format("[Server] EnableCard notify: giveback=%d\n", bGivebackBanTime ? 1 : 0);
        for (int i = 0; i < iCardCount; ++i)
        {
            szMsg += pCard[i];
            szMsg += "\n";
        }
        printf("%s", szMsg.GetString());
        fflush(stdout);
        LeaveCriticalSection(&g_Lock);
        return 0;
    }

    int __stdcall SP_Notify_Callback_DisabledCard_v2(
        char* szSoftwareName,
        char* szOperator,
        char* pCard[],
        int iCardCount,
        char* szCause)
    {
        ensure_global_lock();
        EnterCriticalSection(&g_Lock);
        CStringA szMsg("[Server] DisableCard notify\n");
        for (int i = 0; i < iCardCount; ++i)
        {
            szMsg += pCard[i];
            szMsg += "\n";
        }
        printf("%s", szMsg.GetString());
        fflush(stdout);
        LeaveCriticalSection(&g_Lock);
        return 0;
    }

    void __stdcall SP_CloudComputing_Init_v2(tagInterface_v2* pInterface)
    {
        ensure_global_lock();
        memcpy(&g_Interface, pInterface, sizeof(tagInterface_v2));
        g_Interface.SP_Cloud_SetBufferSizeMax(4096);
        g_ResultBufferMax = 4096;
        // 当前版本仅需要配置结果缓冲区大小
    }

    void __stdcall SP_Notify_Callback_PreUnInit_v2()
    {
        if (g_LockInitialized)
        {
            DeleteCriticalSection(&g_Lock);
            g_LockInitialized = false;
            g_LockInitOnce = INIT_ONCE_STATIC_INIT;
        }
        printf("[Server] Cloud plugin unloading\n");
        fflush(stdout);
    }
}

