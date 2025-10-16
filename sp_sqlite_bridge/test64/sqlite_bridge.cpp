#include "sqlite3.h"
#ifndef SQLITE_TRANSIENT
#define SQLITE_TRANSIENT ((sqlite3_destructor_type)-1)
#endif
#include <cctype>
#include <ctime>
#include "sqlite_bridge.h"
#include <cstdint>
#include <cstdio>
#include <cstdlib>
#include <cstring>
#include <cstdarg>
#include <vector>
#include <string>
#include <exception>
#include <mutex>
#include "common/cloud_crypto.h"
#include "common/cloud_protocol.h"
#include <windows.h>
#include "VMProtectSDK.h"
#include "SPVerify.h"

using std::FILE;

typedef struct sp_query_parameter_entry
{
    int is_text;
    const char* text;
    sqlite3_int64 value;
} sp_query_parameter_entry;
// ---- 静态内部函数的原型声明（避免隐式 int 引发的重定义冲突）----
static void finalize_stmt(sqlite3_stmt** stmt);
static int duplicate_text_column(sqlite3_stmt* stmt, int column_index, char** out_value);
static int duplicate_optional_text_column(sqlite3_stmt* stmt, int column_index, char** out_value);
static int build_in_clause(int count, char** out_sql);
static int find_column_index(sqlite3_stmt* stmt, const char* const* candidates, int candidate_count);
static int get_int_column(sqlite3_stmt* stmt, int column_index);
static long long get_int64_column(sqlite3_stmt* stmt, int column_index);
static double get_double_column(sqlite3_stmt* stmt, int column_index);
static FILE* safe_fopen(const char* path, const char* mode);
static void load_environment_value(const char* name, char* destination, size_t destination_size);
static void convert_error_message_to_utf8(const char* source, char* destination, size_t destination_size);
static void set_error(char** error_message, const char* message, sqlite3* db);
static void set_error_with_details(char** error_message, const char* message, const char* details);
static void set_cloud_error(char** error_message, const char* message, int error_code);
#ifdef _WIN32
static void perform_security_checks(void);
#else
static void perform_security_checks(void)
{
}
#endif
static int add_column_if_missing(
    sqlite3* db,
    const char* table_name,
    const char* column_name,
    const char* alter_sql,
    char** error_message);
static int ensure_blacklist_schema(sqlite3* db, char** error_message);


static CRITICAL_SECTION g_log_lock;
static int g_log_lock_initialized = 0;
static CRITICAL_SECTION g_verification_lock;
static int g_verification_lock_initialized = 0;
static CRITICAL_SECTION g_pic_check_lock;
static int g_pic_check_lock_initialized = 0;
static volatile LONG g_pic_checks_enabled = 0;
static LONG g_next_pic_slot = 0;
static char g_error_log_path[MAX_PATH];
static int g_log_path_initialized = 0;
static volatile LONG g_heartbeat_running = 0;
static HANDLE g_heartbeat_thread = NULL;
static HANDLE g_shutdown_event = NULL;
static int g_verification_state = 0;
static int g_verification_error = SP_UNKNOWNERRROR;
static int g_credentials_initialized = 0;
static char g_connect_key[128] = "";  // Default empty; set via SP_CONNECT_KEY
static std::mutex g_cloud_mutex;

static const char* g_card_number = VMProtectDecryptStringA("YKA6B2415FB855467FA089323295593427");





// 自定义 CloudID, 客户端与服务端保持一致以区分指令类型。
enum class sp_cloud_command : int
{
    test = 1,
    remote_query = 2,
};

static void initialize_log_path(HMODULE module)
{
    char module_path[MAX_PATH];
    DWORD length = GetModuleFileNameA(module, module_path, (DWORD)sizeof(module_path));
    if (length == 0 || length >= (DWORD)sizeof(module_path))
    {
        snprintf(g_error_log_path, sizeof(g_error_log_path), "error.txt");
        g_log_path_initialized = 1;
        return;
    }

    char* last_slash = strrchr(module_path, '\\');
    if (last_slash)
    {
        *(last_slash + 1) = '\0';
    }
    else
    {
        module_path[0] = '\0';
    }

    snprintf(g_error_log_path, sizeof(g_error_log_path), "%serror.txt", module_path);
    g_log_path_initialized = 1;
}

//vmp自动加密逻辑
namespace
{
#ifdef _WIN32
    class VmProtectScope final
    {
    public:
        explicit VmProtectScope(const char* section_name) noexcept
        {
            if (section_name && section_name[0] != '\0')
            {
                VMProtectBegin(section_name);
            }
            else
            {
                VMProtectBegin(NULL);
            }
        }

        VmProtectScope(const VmProtectScope&) = delete;
        VmProtectScope& operator=(const VmProtectScope&) = delete;

        ~VmProtectScope() noexcept
        {
            VMProtectEnd();
        }
    };
#else
    class VmProtectScope final
    {
    public:
        explicit VmProtectScope(const char*) noexcept {}
    };
#endif
} // namespace

#define SP_VMP_CONCAT_IMPL(a, b) a##b
#define SP_VMP_CONCAT(a, b) SP_VMP_CONCAT_IMPL(a, b)
#ifdef _WIN32
#define SP_VMP_SECTION(name_literal) VmProtectScope SP_VMP_CONCAT(vmp_scope_, __COUNTER__)(name_literal)
#else
#define SP_VMP_SECTION(name_literal) ((void)0)
#endif

#ifdef _WIN32

static void write_error_log(const char* format, ...)
{
    if (!format)
    {
        return;
    }

    char message[1024];
    va_list args;
    va_start(args, format);
    int written = vsnprintf(message, sizeof(message), format, args);
    va_end(args);
    if (written < 0)
    {
        return;
    }

    SYSTEMTIME system_time;
    GetLocalTime(&system_time);

    char line[1200];
    int line_length = snprintf(
        line,
        sizeof(line),
        "[%04u-%02u-%02u %02u:%02u:%02u] %s\n",
        (unsigned int)system_time.wYear,
        (unsigned int)system_time.wMonth,
        (unsigned int)system_time.wDay,
        (unsigned int)system_time.wHour,
        (unsigned int)system_time.wMinute,
        (unsigned int)system_time.wSecond,
        message);
    if (line_length <= 0)
    {
        return;
    }

    const char* path = g_log_path_initialized ? g_error_log_path : "error.txt";

    if (g_log_lock_initialized)
    {
        EnterCriticalSection(&g_log_lock);
    }

    FILE* file = safe_fopen(path, "a");
    if (!file && std::strcmp(path, "error.txt") != 0)
    {
        file = safe_fopen("error.txt", "a");
    }

    if (file)
    {
        std::fwrite(line, 1, (size_t)line_length, file);
        std::fclose(file);
    }

    if (g_log_lock_initialized)
    {
        LeaveCriticalSection(&g_log_lock);
    }
}

static void log_error_code(const char* context, int error)
{
    char detail[256] = {0};
    SP_Verify_GetErrorMsg(error, detail);

    char detail_utf8[512] = {0};
    convert_error_message_to_utf8(detail, detail_utf8, sizeof(detail_utf8));

    if (detail_utf8[0] != '\0')
    {
        write_error_log("%s: %s (code %d)", context, detail_utf8, error);
    }
    else
    {
        write_error_log("%s: code %d", context, error);
    }
}

// 说明：
// 1) 仅在“逻辑性强但无异常抛出”的片段放置 VMP 段；
// 2) 加/解密调用、try/catch、STL 分配器附近不放 VMP；
// 3) 增加了响应长度上限与空返回保护，避免异常数据导致的后续崩溃；
// 4) 若你在 VMProtect 里给不同段配置不同模式，建议：
//    - Cloud_Prepare / Cloud_Send / Cloud_End 用 Mutation（或轻量 Virtualization）；
//    - crypto 内核函数（AES/SHA）不要加 VMP。

static bool send_cloud_request(
    sp::CloudQuery query,
    const std::vector<std::uint8_t>& payload,
    std::vector<std::uint8_t>& response,
    char** error_message)
{
    // ========== 预备阶段：仅做轻量逻辑，允许放 VMP ==========
    {
        SP_VMP_SECTION("Cloud_Prepare"); // ✅ 安全：无异常抛出、无复杂分配
        (void)error_message; // 防未用参数的编译器噪音（若你会用到就删掉这行）
    }

    // 组包: [协议版本][指令类型][payload内容]
    std::vector<std::uint8_t> plain;
    plain.reserve(sizeof(std::uint32_t) * 2 + payload.size());
    sp::BinaryWriter writer(plain);
    writer.write_uint32(sp::kCloudProtocolVersion);
    writer.write_uint32(static_cast<std::uint32_t>(query));
    writer.write_bytes(payload);

    // ========== 加密阶段：不要在 try/catch 里放 VMP ==========
    std::vector<std::uint8_t> encrypted;
    try
    {
        // ❌ 不加 VMP：crypto 内含异常/内存/汇编优化，虚拟化易破坏调用约定/异常表
        encrypted = sp::crypto::encrypt_packet(g_card_number, plain);
    }
    catch (const std::exception& ex)
    {
        set_error_with_details(error_message, "Failed to encrypt cloud payload", ex.what());
        return false;
    }

    std::vector<std::uint8_t> encrypted_response;
    {
        std::lock_guard<std::mutex> lock(g_cloud_mutex);

        // ========== 发送阶段：可以把“调用前后的包装逻辑”放 VMP ==========
        {
            SP_VMP_SECTION("Cloud_Send"); // ✅ 安全：只包裹外层流程控制
            int error = 0;

            // 说明：SP_CloudComputing 的 buffer 参数是非 const 的，按你原码使用 const_cast 即可。
            if (!SP_CloudComputing(
                static_cast<int>(sp_cloud_command::remote_query),
                const_cast<unsigned char*>(encrypted.data()),
                static_cast<int>(encrypted.size()),
                &error))
            {
                log_error_code("SP_CloudComputing", error);
                set_cloud_error(error_message, "Cloud communication failed", error);
                return false;
            }
        }

        // 读取长度（不放 VMP，保持调用 ABI 稳定）
        int length = SP_CloudResultLength(static_cast<int>(sp_cloud_command::remote_query));
        if (length <= 0)
        {
            set_error(error_message, "Cloud response is empty", NULL);
            return false;
        }

        // 可选：做一个合理的上限，防御异常包尺寸（避免后续内存分配过大）
        // 例如 8MB 上限，可根据你协议调大或去掉
        constexpr int kMaxCloudPacket = 8 * 1024 * 1024;
        if (length > kMaxCloudPacket)
        {
            set_error(error_message, "Cloud response is too large", NULL);
            return false;
        }

        encrypted_response.resize(static_cast<std::size_t>(length));
        // 读取结果（依旧不放 VMP，避免第三方/SDK 调用栈被改写）
        SP_CloudResult(
            static_cast<int>(sp_cloud_command::remote_query),
            encrypted_response.data(),
            length);
    }

    // ========== 解密阶段：仍然不要在 try/catch 内放 VMP ==========
    try
    {
        // ❌ 不加 VMP：与加密同理
        response = sp::crypto::decrypt_packet(g_card_number, encrypted_response);
    }
    catch (const std::exception& ex)
    {
        // 这里曾出现 “Packet size mismatch”等错误，通常是因为把 try/catch 包进 VMP 造成异常表/栈破坏
        set_error_with_details(error_message, "Failed to decrypt cloud response", ex.what());
        return false;
    }

    // ========== 收尾阶段：仅做轻量逻辑，可放 VMP ==========
    {
        SP_VMP_SECTION("Cloud_End"); // ✅ 安全：无异常、无分配
    }

    return true;
}


struct cloud_sql_instruction
{
    bool expects_result_set = false;
    std::string sql;
};

static int parse_cloud_sql_instruction(
    const std::vector<std::uint8_t>& response,
    cloud_sql_instruction* instruction,
    char** error_message)
{
    SP_VMP_SECTION("parse_cloud_sql_instruction");
    if (!instruction)
    {
        set_error(error_message, "Invalid cloud instruction", NULL);
        return SQLITE_ERROR;
    }

    try
    {
        sp::BinaryReader reader(response.data(), response.data() + response.size());
        int rc = static_cast<int>(reader.read_uint32());
        if (rc != SQLITE_OK)
        {
            std::string message = reader.read_string();
            set_error(error_message, message.c_str(), NULL);
            return rc;
        }

        instruction->expects_result_set = reader.read_uint32() != 0;
        instruction->sql = reader.read_string();
        return SQLITE_OK;
    }
    catch (const std::exception& ex)
    {
        set_error_with_details(error_message, "Failed to parse cloud response", ex.what());
        return SQLITE_ERROR;
    }
}

static void safe_string_copy(char* destination, size_t destination_size, const char* source)
{
    if (!destination || destination_size == 0)
    {
        return;
    }

#if defined(_MSC_VER)
    if (!source)
    {
        destination[0] = '\0';
        return;
    }

    strncpy_s(destination, destination_size, source, _TRUNCATE);
#else
    if (!source)
    {
        destination[0] = '\0';
        return;
    }

    std::strncpy(destination, source, destination_size - 1);
    destination[destination_size - 1] = '\0';
#endif
}

// 将 std::string 拷贝为结构体需要的 C 字符串，调用方负责释放。
static int duplicate_string_value(const std::string& source, char** out_value)
{
    if (!out_value)
    {
        return SQLITE_ERROR;
    }

    char* buffer = static_cast<char*>(std::malloc(source.size() + 1));
    if (!buffer)
    {
        return SQLITE_NOMEM;
    }

    std::memcpy(buffer, source.c_str(), source.size() + 1);
    *out_value = buffer;
    return SQLITE_OK;
}

static FILE* safe_fopen(const char* path, const char* mode)
{
#if defined(_MSC_VER)
    FILE* file = nullptr;
    if (fopen_s(&file, path, mode) != 0)
    {
        return nullptr;
    }
    return file;
#else
    return std::fopen(path, mode);
#endif
}

static void convert_error_message_to_utf8(const char* source, char* destination, size_t destination_size)
{
    if (!destination || destination_size == 0)
    {
        return;
    }

    destination[0] = '\0';

#if defined(_WIN32)
    if (!source || source[0] == '\0')
    {
        return;
    }

    int wide_length = MultiByteToWideChar(CP_ACP, 0, source, -1, nullptr, 0);
    if (wide_length <= 0)
    {
        safe_string_copy(destination, destination_size, source);
        return;
    }

    std::vector<wchar_t> wide_buffer(static_cast<size_t>(wide_length));
    int wide_written = MultiByteToWideChar(CP_ACP, 0, source, -1, wide_buffer.data(), wide_length);
    if (wide_written <= 0)
    {
        safe_string_copy(destination, destination_size, source);
        return;
    }

    int utf8_length = WideCharToMultiByte(CP_UTF8, 0, wide_buffer.data(), wide_written, nullptr, 0, nullptr, nullptr);
    if (utf8_length <= 0)
    {
        safe_string_copy(destination, destination_size, source);
        return;
    }

    std::vector<char> utf8_buffer(static_cast<size_t>(utf8_length));
    int utf8_written = WideCharToMultiByte(CP_UTF8, 0, wide_buffer.data(), wide_written, utf8_buffer.data(), utf8_length, nullptr, nullptr);
    if (utf8_written <= 0)
    {
        safe_string_copy(destination, destination_size, source);
        return;
    }

    safe_string_copy(destination, destination_size, utf8_buffer.data());
#else
    if (source)
    {
        safe_string_copy(destination, destination_size, source);
    }
#endif
}

static void load_environment_value(const char* name, char* destination, size_t destination_size)
{
    if (!name || !destination || destination_size == 0)
    {
        return;
    }

#if defined(_MSC_VER)
    char* value = nullptr;
    size_t value_length = 0;
    if (_dupenv_s(&value, &value_length, name) == 0 && value)
    {
        if (value[0] != '\0')
        {
            safe_string_copy(destination, destination_size, value);
        }
        free(value);
    }
#else
    const char* value = std::getenv(name);
    if (value && value[0] != '\0')
    {
        safe_string_copy(destination, destination_size, value);
    }
#endif
}

static void initialize_credentials(void)
{
    if (g_credentials_initialized)
    {
        return;
    }

    load_environment_value("SP_CONNECT_KEY", g_connect_key, sizeof(g_connect_key));
    void load_environment_value(const char* key, const char* buffer, size_t size);


    g_credentials_initialized = 1;
}

static int perform_card_login(void)
{
    
    write_error_log("Attempting card login.");
    int error = SP_Verify_CardLogin(g_card_number);
    if (error == SP_NOERROR)
    {
        write_error_log("Card login succeeded.");
        return SP_NOERROR;
    }

    log_error_code("Card login failed", error);
    return error;
}

static void handle_heartbeat_error(int error)
{
    log_error_code("Heartbeat error", error);

    if (error == SP_CONNECTFAILED || error == SP_EXPIREDTOKEN || error == SP_UNKNOWNERRROR)
    {
        for (int attempt = 0; attempt < 3; ++attempt)
        {
            Sleep(10 * 1000);
            int login_error = perform_card_login();
            if (login_error == SP_NOERROR)
            {
                write_error_log("Heartbeat recovery succeeded on attempt %d.", attempt + 1);
                return;
            }

            log_error_code("Heartbeat recovery failed", login_error);
        }
    }

    write_error_log("Unrecoverable network verification error (%d). Terminating process.", error);
    ExitProcess(0);
}

static DWORD WINAPI heartbeat_thread_proc(LPVOID parameter)
{
    (void)parameter;
    VMProtectBegin("Thread_HeartBeat");

    const DWORD heartbeat_interval_ms = 30 * 1000;
    const DWORD wait_slice_ms = 1000;
    ULONGLONG next_heartbeat_deadline = GetTickCount64();

    while (g_heartbeat_running)
    {
        ULONGLONG now = GetTickCount64();
        if (now >= next_heartbeat_deadline)
        {
            int error = 0;
           
            if (!SP_Cloud_Beat(&error))
            {
                handle_heartbeat_error(error);
            }

            if (!g_heartbeat_running)
            {
                break;
            }

            next_heartbeat_deadline = now + heartbeat_interval_ms;
            continue;
        }

        DWORD remaining_ms = (DWORD)(next_heartbeat_deadline - now);
        DWORD wait_ms = remaining_ms < wait_slice_ms ? remaining_ms : wait_slice_ms;
        if (wait_ms == 0)
        {
            continue;
        }

        DWORD wait_result = WAIT_TIMEOUT;
        if (g_shutdown_event)
        {
            wait_result = WaitForSingleObject(g_shutdown_event, wait_ms);
        }
        else
        {
            Sleep(wait_ms);
        }

        if (wait_result == WAIT_OBJECT_0)
        {
            break;
        }
        if (wait_result == WAIT_FAILED)
        {
            DWORD last_error = GetLastError();
            write_error_log("Heartbeat wait failed. Win32 error: %lu", (unsigned long)last_error);
            Sleep(1000);
        }
    }

    VMProtectEnd();
    return 0;
}

static void format_verification_error(int error, char* buffer, size_t buffer_size)
{
    if (!buffer || buffer_size == 0)
    {
        return;
    }

    char detail[256] = {0};
    SP_Verify_GetErrorMsg(error, detail);
    char detail_utf8[512] = {0};
    convert_error_message_to_utf8(detail, detail_utf8, sizeof(detail_utf8));

    if (detail_utf8[0] != '\0')
    {
        snprintf(buffer, buffer_size, "Network verification failed (%d): %s", error, detail_utf8);
    }
    else
    {
        snprintf(buffer, buffer_size, "Network verification failed (%d)", error);
    }
}

static int initialize_network_verification_internal(void)
{
    initialize_credentials();
    VMProtectBegin("sqlite_bridge_network_init");

    int error = SP_Verify_Init("z9FbLtsSYzwgQYpjvPWyTVWDKLEyOYuvbBMZkh4gVfvMi0/W2SvilWsRRumZCnNjjJ8UlbwutD0nglSLt9RKVnH0AKu3cagqQOx3wTay/dVjD6+caZ//59UfQ1b3XmCDWxdfdnAGOUJuj1VUxeH+JfSnm3syqtOixuP6Z+Y409alJT4wgomsYcY3ZGvYouPYlHxNVO+4+hH/zJrqv87aFYMZ9x6pIDR4EC2Ka5tAx4n+Ie3/2+AwWn2zNcRCrZ3r", 30 * 1000, false);
    if (error != SP_NOERROR)
    {
        log_error_code("SP_Verify_Init failed", error);
        VMProtectEnd();
        return error;
    }

    SP_Cloud_PICEnable();
    g_next_pic_slot = 0;
    InterlockedExchange(&g_pic_checks_enabled, 1);
   

    if (SP_Verify_AntiDebugger())
    {
        SP_Verify_DisablePCSign();
        SP_Verify_DisableIP();
        write_error_log("Anti-debugger countermeasures applied.");
    }

    error = perform_card_login();
    if (error != SP_NOERROR)
    {
        g_next_pic_slot = 0;
        InterlockedExchange(&g_pic_checks_enabled, 0);
        VMProtectEnd();
        return error;
    }

  
    if (!SP_Verify_IsLogin())
    {
        write_error_log("Login verification check failed after successful authentication.");
        g_next_pic_slot = 0;
        InterlockedExchange(&g_pic_checks_enabled, 0);
        VMProtectEnd();
        return SP_UNKNOWNERRROR;
    }

    g_shutdown_event = CreateEvent(NULL, TRUE, FALSE, NULL);
    if (!g_shutdown_event)
    {
        DWORD last_error = GetLastError();
        write_error_log("Failed to create heartbeat shutdown event. Win32 error: %lu", (unsigned long)last_error);
        g_next_pic_slot = 0;
        InterlockedExchange(&g_pic_checks_enabled, 0);
        VMProtectEnd();
        return SP_UNKNOWNERRROR;
    }

    g_heartbeat_running = 1;
    g_heartbeat_thread = CreateThread(NULL, 0, heartbeat_thread_proc, NULL, 0, NULL);
    if (!g_heartbeat_thread)
    {
        DWORD last_error = GetLastError();
        g_heartbeat_running = 0;
        CloseHandle(g_shutdown_event);
        g_shutdown_event = NULL;
        write_error_log("Failed to create heartbeat thread. Win32 error: %lu", (unsigned long)last_error);
        g_next_pic_slot = 0;
        InterlockedExchange(&g_pic_checks_enabled, 0);
        VMProtectEnd();
        return SP_UNKNOWNERRROR;
    }

    write_error_log("Network verification initialized successfully.");
    VMProtectEnd();
    return SP_NOERROR;
}

static int ensure_network_verification_ready(char** error_message)
{
    if (!g_verification_lock_initialized)
    {
        return 1;
    }

    int result = SP_NOERROR;
    int is_ready = 0;

    EnterCriticalSection(&g_verification_lock);
    if (g_verification_state == 1)
    {
        is_ready = 1;
        LeaveCriticalSection(&g_verification_lock);
        goto ready_exit;
    }

    if (g_verification_state == 0)
    {
        result = initialize_network_verification_internal();
        if (result == SP_NOERROR)
        {
            g_verification_state = 1;
            is_ready = 1;
        }
        else
        {
            g_verification_state = -1;
            g_verification_error = result;
        }
    }
    else
    {
        result = g_verification_error;
    }

    LeaveCriticalSection(&g_verification_lock);

ready_exit:
    if (result == SP_NOERROR && is_ready)
    {
        return 1;
    }

    char message[256] = {0};
    format_verification_error(result, message, sizeof(message));
    write_error_log("%s", message);
    if (error_message)
    {
        size_t length = strlen(message);
        char* copy = (char*)malloc(length + 1);
        if (copy)
        {
            memcpy(copy, message, length + 1);
            *error_message = copy;
        }
    }

    return 0;
}

BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID reserved)
{
    (void)reserved;

    switch (reason)
    {
        case DLL_PROCESS_ATTACH:
            DisableThreadLibraryCalls(instance);
            if (!g_log_lock_initialized)
            {
                InitializeCriticalSection(&g_log_lock);
                g_log_lock_initialized = 1;
            }
            if (!g_verification_lock_initialized)
            {
                InitializeCriticalSection(&g_verification_lock);
                g_verification_lock_initialized = 1;
            }
            if (!g_pic_check_lock_initialized)
            {
                InitializeCriticalSection(&g_pic_check_lock);
                g_pic_check_lock_initialized = 1;
            }
            initialize_log_path(instance);
            break;

        case DLL_PROCESS_DETACH:
            g_heartbeat_running = 0;
            InterlockedExchange(&g_pic_checks_enabled, 0);
            if (g_shutdown_event)
            {
                SetEvent(g_shutdown_event);
            }
            if (g_heartbeat_thread)
            {
                WaitForSingleObject(g_heartbeat_thread, INFINITE);
                CloseHandle(g_heartbeat_thread);
                g_heartbeat_thread = NULL;
            }
            if (g_shutdown_event)
            {
                CloseHandle(g_shutdown_event);
                g_shutdown_event = NULL;
            }
            if (g_verification_lock_initialized)
            {
                DeleteCriticalSection(&g_verification_lock);
                g_verification_lock_initialized = 0;
            }
            if (g_pic_check_lock_initialized)
            {
                DeleteCriticalSection(&g_pic_check_lock);
                g_pic_check_lock_initialized = 0;
            }
            if (g_log_lock_initialized)
            {
                DeleteCriticalSection(&g_log_lock);
                g_log_lock_initialized = 0;
            }
            break;
    }

    return TRUE;
}

#else
static int ensure_network_verification_ready(char** error_message)
{
    (void)error_message;
    return 1;
}
#endif

static void set_error(char** error_message, const char* message, sqlite3* db)
{
    if (!error_message)
    {
        return;
    }

    if (message)
    {
        size_t length = strlen(message);
        char* copy = (char*)malloc(length + 1);
        if (copy)
        {
            memcpy(copy, message, length + 1);
            *error_message = copy;
            return;
        }
    }

    if (db)
    {
        const char* sqlite_message = sqlite3_errmsg(db);
        if (sqlite_message)
        {
            size_t length = strlen(sqlite_message);
            char* copy = (char*)malloc(length + 1);
            if (copy)
            {
                memcpy(copy, sqlite_message, length + 1);
                *error_message = copy;
                return;
            }
        }
    }

    *error_message = NULL;
}

static void set_error_with_details(char** error_message, const char* message, const char* details)
{
    if (!error_message)
    {
        return;
    }

    if (message)
    {
        std::string combined = message;
        if (details && details[0] != '\0')
        {
            combined += ": ";
            combined += details;
        }

        set_error(error_message, combined.c_str(), NULL);
        return;
    }

    if (details)
    {
        set_error(error_message, details, NULL);
        return;
    }

    *error_message = NULL;
}

static void set_cloud_error(char** error_message, const char* message, int error_code)
{
    if (!error_message)
    {
        return;
    }

    char detail[256] = {0};
    if (error_code != 0)
    {
        SP_Verify_GetErrorMsg(error_code, detail);
    }

    char detail_utf8[512] = {0};
    if (detail[0] != '\0')
    {
        convert_error_message_to_utf8(detail, detail_utf8, sizeof(detail_utf8));
    }

    char formatted[1024] = {0};
    if (detail_utf8[0] != '\0')
    {
        std::snprintf(formatted, sizeof(formatted), "%s (code %d: %s)", message, error_code, detail_utf8);
    }
    else if (error_code != 0)
    {
        std::snprintf(formatted, sizeof(formatted), "%s (code %d)", message, error_code);
    }
    else
    {
        std::snprintf(formatted, sizeof(formatted), "%s", message);
    }

    set_error(error_message, formatted, NULL);
}

static void set_sqlite_error(char** error_message, sqlite3* db)
{
    if (!error_message)
    {
        return;
    }

    const char* sqlite_message = db ? sqlite3_errmsg(db) : "Unknown SQLite error";
    size_t length = strlen(sqlite_message);
    char* copy = (char*)malloc(length + 1);
    if (copy)
    {
        memcpy(copy, sqlite_message, length + 1);
        *error_message = copy;
    }
    else
    {
        *error_message = NULL;
    }
}

static int equals_ignore_case(const char* a, const char* b)
{
    if (!a || !b)
    {
        return 0;
    }

    while (*a && *b)
    {
        if (tolower((unsigned char)*a) != tolower((unsigned char)*b))
        {
            return 0;
        }

        a++;
        b++;
    }

    return *a == '\0' && *b == '\0';
}

static void trim_whitespace_in_place(char* text)
{
    if (!text)
    {
        return;
    }

    char* start = text;
    while (*start && isspace((unsigned char)*start))
    {
        start++;
    }

    char* end = start + strlen(start);
    while (end > start && isspace((unsigned char)*(end - 1)))
    {
        end--;
    }

    size_t length = (size_t)(end - start);
    if (start != text && length > 0)
    {
        memmove(text, start, length);
    }
    text[length] = '\0';
}

static char* duplicate_trimmed_text(const unsigned char* text)
{
    if (!text)
    {
        char* empty = (char*)malloc(1);
        if (empty)
        {
            empty[0] = '\0';
        }

        return empty;
    }

    size_t original_length = strlen((const char*)text);
    char* copy = (char*)malloc(original_length + 1);
    if (!copy)
    {
        return NULL;
    }

    memcpy(copy, text, original_length + 1);
    trim_whitespace_in_place(copy);
    return copy;
}

static int table_exists(sqlite3* db, const char* table_name, int* exists)
{
    if (!db || !table_name || !exists)
    {
        return SQLITE_ERROR;
    }

    *exists = 0;
    const char* sql = "SELECT 1 FROM sqlite_master WHERE type='table' AND lower(name)=lower(?) LIMIT 1;";
    sqlite3_stmt* stmt = NULL;
    int rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    sqlite3_bind_text(stmt, 1, table_name, -1, SQLITE_TRANSIENT);
    rc = sqlite3_step(stmt);
    if (rc == SQLITE_ROW)
    {
        *exists = 1;
        rc = SQLITE_OK;
    }
    else if (rc == SQLITE_DONE)
    {
        rc = SQLITE_OK;
    }

    finalize_stmt(&stmt);
    return rc;
}

static int table_has_column(sqlite3* db, const char* table_name, const char* column_name, int* has_column)
{
    if (!db || !table_name || !column_name || !has_column)
    {
        return SQLITE_ERROR;
    }

    *has_column = 0;
    char* sql = sqlite3_mprintf("PRAGMA table_info(%Q);", table_name);
    if (!sql)
    {
        return SQLITE_NOMEM;
    }

    sqlite3_stmt* stmt = NULL;
    int rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    sqlite3_free(sql);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    while ((rc = sqlite3_step(stmt)) == SQLITE_ROW)
    {
        const unsigned char* name = sqlite3_column_text(stmt, 1);
        if (equals_ignore_case((const char*)name, column_name))
        {
            *has_column = 1;
            rc = SQLITE_OK;
            break;
        }
    }

    if (rc == SQLITE_DONE)
    {
        rc = SQLITE_OK;
    }
    else if (rc == SQLITE_ROW)
    {
        rc = SQLITE_OK;
    }

    finalize_stmt(&stmt);
    return rc;
}

static void free_blacklist_machines(sp_blacklist_machine_record* records, int count)
{
    if (!records)
    {
        return;
    }

    for (int i = 0; i < count; i++)
    {
        free(records[i].value);
        free(records[i].remarks);
    }

    free(records);
}

static void free_blacklist_logs(sp_blacklist_log_record* records, int count)
{
    if (!records)
    {
        return;
    }

    for (int i = 0; i < count; i++)
    {
        free(records[i].ip);
        free(records[i].card);
        free(records[i].pcsign);
        free(records[i].err_events);
    }

    free(records);
}

static void free_agent_record(sp_agent_record* record)
{
    if (!record)
    {
        return;
    }

    free(record->user);
    free(record->password);
    free(record->duration);
    free(record->authority);
    free(record->card_type_auth_name);
    free(record->remarks);
    free(record->fnode);
}

static void free_agent_records(sp_agent_record* records, int count)
{
    if (!records)
    {
        return;
    }

    for (int i = 0; i < count; i++)
    {
        free_agent_record(&records[i]);
    }

    free(records);
}

static int populate_agent_record(sqlite3_stmt* stmt, sp_agent_record* record)
{
    if (!stmt || !record)
    {
        return SQLITE_ERROR;
    }

    memset(record, 0, sizeof(*record));

    if (duplicate_text_column(stmt, 0, &record->user) != SQLITE_OK ||
        duplicate_text_column(stmt, 1, &record->password) != SQLITE_OK ||
        duplicate_text_column(stmt, 4, &record->duration) != SQLITE_OK ||
        duplicate_text_column(stmt, 5, &record->authority) != SQLITE_OK ||
        duplicate_text_column(stmt, 6, &record->card_type_auth_name) != SQLITE_OK ||
        duplicate_text_column(stmt, 8, &record->remarks) != SQLITE_OK ||
        duplicate_text_column(stmt, 9, &record->fnode) != SQLITE_OK)
    {
        free_agent_record(record);
        return SQLITE_NOMEM;
    }

    record->account_balance = sqlite3_column_double(stmt, 2);
    record->account_time = sqlite3_column_int64(stmt, 3);
    record->cards_enable = sqlite3_column_int(stmt, 7);
    record->stat = sqlite3_column_int(stmt, 10);
    record->deleted_at = sqlite3_column_int(stmt, 11);
    record->duration_raw = sqlite3_column_int64(stmt, 12);
    record->parities = sqlite3_column_double(stmt, 13);
    record->total_parities = sqlite3_column_double(stmt, 14);

    return SQLITE_OK;
}

static int ensure_usage_distribution_schema(sqlite3* db, char** error_message)
{
    static const char* sql =
        "CREATE TABLE IF NOT EXISTS UsageDistributionCache ("
        "    Software TEXT NOT NULL,"
        "    Whom TEXT NOT NULL,"
        "    Payload TEXT NOT NULL,"
        "    ResolvedTotal INTEGER NOT NULL,"
        "    UpdatedAt INTEGER NOT NULL,"
        "    PRIMARY KEY (Software, Whom)"
        ");";

    char* errmsg = NULL;
    int rc = sqlite3_exec(db, sql, NULL, NULL, &errmsg);
    if (rc != SQLITE_OK)
    {
        if (errmsg)
        {
            set_error(error_message, errmsg, db);
            sqlite3_free(errmsg);
        }
        else
        {
            set_sqlite_error(error_message, db);
        }
    }

    return rc;
}

static int add_column_if_missing(
    sqlite3* db,
    const char* table_name,
    const char* column_name,
    const char* alter_sql,
    char** error_message)
{
    if (!db || !table_name || !column_name || !alter_sql)
    {
        set_error(error_message, "Invalid schema arguments", db);
        return SQLITE_ERROR;
    }

    int has_column = 0;
    int rc = table_has_column(db, table_name, column_name, &has_column);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        return rc;
    }

    if (has_column)
    {
        return SQLITE_OK;
    }

    char* errmsg = NULL;
    rc = sqlite3_exec(db, alter_sql, NULL, NULL, &errmsg);
    if (rc != SQLITE_OK)
    {
        if (errmsg)
        {
            set_error(error_message, errmsg, db);
            sqlite3_free(errmsg);
        }
        else
        {
            set_sqlite_error(error_message, db);
        }

        int refreshed = 0;
        int refresh_rc = table_has_column(db, table_name, column_name, &refreshed);
        if (refresh_rc == SQLITE_OK && refreshed)
        {
            if (error_message && *error_message)
            {
                free(*error_message);
                *error_message = NULL;
            }

            return SQLITE_OK;
        }

        if (refresh_rc != SQLITE_OK)
        {
            if (!error_message || !*error_message)
            {
                set_sqlite_error(error_message, db);
            }

            return refresh_rc;
        }

        return rc;
    }

    return SQLITE_OK;
}

static int ensure_blacklist_schema(sqlite3* db, char** error_message)
{
    if (!db)
    {
        set_error(error_message, "Invalid database handle", NULL);
        return SQLITE_ERROR;
    }

    int exists = 0;
    int rc = table_exists(db, "Blacklist", &exists);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        return rc;
    }

    if (!exists)
    {
        const char* create_sql =
            "CREATE TABLE IF NOT EXISTS Blacklist ("
            "    Value TEXT PRIMARY KEY,"
            "    Type INTEGER DEFAULT 0,"
            "    Remarks TEXT"
            ");";

        char* errmsg = NULL;
        rc = sqlite3_exec(db, create_sql, NULL, NULL, &errmsg);
        if (rc != SQLITE_OK)
        {
            if (errmsg)
            {
                set_error(error_message, errmsg, db);
                sqlite3_free(errmsg);
            }
            else
            {
                set_sqlite_error(error_message, db);
            }

            return rc;
        }
    }

    rc = add_column_if_missing(
        db,
        "Blacklist",
        "Type",
        "ALTER TABLE Blacklist ADD COLUMN Type INTEGER DEFAULT 0;",
        error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    rc = add_column_if_missing(
        db,
        "Blacklist",
        "Remarks",
        "ALTER TABLE Blacklist ADD COLUMN Remarks TEXT;",
        error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    return SQLITE_OK;
}

static int ensure_ip_location_schema(sqlite3* db, char** error_message)
{
    static const char* sql =
        "CREATE TABLE IF NOT EXISTS IpLocationCache ("
        "    Ip TEXT PRIMARY KEY,"
        "    Province TEXT,"
        "    City TEXT,"
        "    District TEXT,"
        "    UpdatedAt INTEGER NOT NULL"
        ");";

    char* errmsg = NULL;
    int rc = sqlite3_exec(db, sql, NULL, NULL, &errmsg);
    if (rc != SQLITE_OK)
    {
        if (errmsg)
        {
            set_error(error_message, errmsg, db);
            sqlite3_free(errmsg);
        }
        else
        {
            set_sqlite_error(error_message, db);
        }
    }

    return rc;
}

static int open_database(const char* db_path, sqlite3** out_db, char** error_message)
{
    if (!db_path || !out_db)
    {
        set_error(error_message, "Invalid database path", NULL);
        return SQLITE_ERROR;
    }

    
    if (!ensure_network_verification_ready(error_message))
    {
        return SQLITE_ERROR;
    }

    sqlite3* db = NULL;
    int flags = SQLITE_OPEN_READWRITE | SQLITE_OPEN_CREATE | SQLITE_OPEN_FULLMUTEX;
    int rc = sqlite3_open_v2(db_path, &db, flags, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        if (db)
        {
            sqlite3_close(db);
        }
        return rc;
    }

    *out_db = db;
    return SQLITE_OK;
}

static void finalize_stmt(sqlite3_stmt** stmt)
{
    if (stmt && *stmt)
    {
        sqlite3_finalize(*stmt);
        *stmt = NULL;
    }
}

static int begin_transaction(sqlite3* db, char** error_message)
{
    char* errmsg = NULL;
    int rc = sqlite3_exec(db, "BEGIN IMMEDIATE TRANSACTION;", NULL, NULL, &errmsg);
    if (rc != SQLITE_OK)
    {
        if (errmsg)
        {
            set_error(error_message, errmsg, db);
            sqlite3_free(errmsg);
        }
        else
        {
            set_sqlite_error(error_message, db);
        }
    }

    return rc;
}

static int commit_transaction(sqlite3* db, char** error_message)
{
    char* errmsg = NULL;
    int rc = sqlite3_exec(db, "COMMIT;", NULL, NULL, &errmsg);
    if (rc != SQLITE_OK)
    {
        if (errmsg)
        {
            set_error(error_message, errmsg, db);
            sqlite3_free(errmsg);
        }
        else
        {
            set_sqlite_error(error_message, db);
        }
    }

    return rc;
}

static void rollback_transaction(sqlite3* db)
{
    if (!db)
    {
        return;
    }

    char* errmsg = NULL;
    sqlite3_exec(db, "ROLLBACK;", NULL, NULL, &errmsg);
    if (errmsg)
    {
        sqlite3_free(errmsg);
    }
}

SP_EXPORT int sp_usage_distribution_replace(
    const char* db_path,
    const char* software,
    const sp_usage_distribution_entry* entries,
    int entry_count,
    char** error_message)
{
    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    rc = ensure_usage_distribution_schema(db, error_message);
    if (rc != SQLITE_OK)
    {
        sqlite3_close(db);
        return rc;
    }

    rc = begin_transaction(db, error_message);
    if (rc != SQLITE_OK)
    {
        sqlite3_close(db);
        return rc;
    }

    sqlite3_stmt* delete_stmt = NULL;
    sqlite3_stmt* insert_stmt = NULL;

    rc = sqlite3_prepare_v2(db, "DELETE FROM UsageDistributionCache WHERE Software = ?;", -1, &delete_stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        goto cleanup;
    }

    rc = sqlite3_bind_text(delete_stmt, 1, software ? software : "", -1, SQLITE_TRANSIENT);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        goto cleanup;
    }

    rc = sqlite3_step(delete_stmt);
    if (rc != SQLITE_DONE)
    {
        set_sqlite_error(error_message, db);
        goto cleanup;
    }

    finalize_stmt(&delete_stmt);

    if (entries && entry_count > 0)
    {
        const char* insert_sql =
            "INSERT INTO UsageDistributionCache (Software, Whom, Payload, ResolvedTotal, UpdatedAt) "
            "VALUES (?, ?, ?, ?, ?);";
        rc = sqlite3_prepare_v2(db, insert_sql, -1, &insert_stmt, NULL);
        if (rc != SQLITE_OK)
        {
            set_sqlite_error(error_message, db);
            goto cleanup;
        }

        for (int i = 0; i < entry_count; ++i)
        {
            const sp_usage_distribution_entry* entry = &entries[i];

            sqlite3_reset(insert_stmt);
            sqlite3_clear_bindings(insert_stmt);

            rc = sqlite3_bind_text(insert_stmt, 1, software ? software : "", -1, SQLITE_TRANSIENT);
            if (rc != SQLITE_OK)
            {
                set_sqlite_error(error_message, db);
                goto cleanup;
            }

            rc = sqlite3_bind_text(insert_stmt, 2, entry->whom ? entry->whom : "", -1, SQLITE_TRANSIENT);
            if (rc != SQLITE_OK)
            {
                set_sqlite_error(error_message, db);
                goto cleanup;
            }

            rc = sqlite3_bind_text(insert_stmt, 3, entry->payload ? entry->payload : "", -1, SQLITE_TRANSIENT);
            if (rc != SQLITE_OK)
            {
                set_sqlite_error(error_message, db);
                goto cleanup;
            }

            rc = sqlite3_bind_int64(insert_stmt, 4, entry->resolved_total);
            if (rc != SQLITE_OK)
            {
                set_sqlite_error(error_message, db);
                goto cleanup;
            }

            rc = sqlite3_bind_int64(insert_stmt, 5, entry->updated_at);
            if (rc != SQLITE_OK)
            {
                set_sqlite_error(error_message, db);
                goto cleanup;
            }

            rc = sqlite3_step(insert_stmt);
            if (rc != SQLITE_DONE)
            {
                set_sqlite_error(error_message, db);
                goto cleanup;
            }
        }
    }

    rc = commit_transaction(db, error_message);

cleanup:
    if (rc != SQLITE_OK)
    {
        rollback_transaction(db);
    }

    finalize_stmt(&delete_stmt);
    finalize_stmt(&insert_stmt);

    sqlite3_close(db);
    return rc;
}

static int build_in_clause(int count, char** out_sql)
{
    if (count <= 0 || !out_sql)
    {
        return SQLITE_ERROR;
    }

    size_t length = (size_t)count * 2; // "?," for each, final replaced with ')'
    char* buffer = (char*)malloc(length + 1);
    if (!buffer)
    {
        return SQLITE_NOMEM;
    }

    char* ptr = buffer;
    for (int i = 0; i < count; ++i)
    {
        *ptr++ = '?';
        if (i < count - 1)
        {
            *ptr++ = ',';
        }
    }

    *ptr = '\0';
    *out_sql = buffer;
    return SQLITE_OK;
}

static char* build_or_like_clause(const char* column, int count)
{
    if (!column || count <= 0)
    {
        return NULL;
    }

    char* result = sqlite3_mprintf("%s LIKE ?", column);
    if (!result)
    {
        return NULL;
    }

    for (int i = 1; i < count; ++i)
    {
        char* next = sqlite3_mprintf("%s OR %s LIKE ?", result, column);
        sqlite3_free(result);
        if (!next)
        {
            return NULL;
        }
        result = next;
    }

    return result;
}

static int duplicate_text_column(sqlite3_stmt* stmt, int column_index, char** out_value)
{
    if (!out_value)
    {
        return SQLITE_ERROR;
    }

    const unsigned char* text = sqlite3_column_text(stmt, column_index);
    if (!text)
    {
        *out_value = NULL;
        return SQLITE_OK;
    }

    int length = sqlite3_column_bytes(stmt, column_index);
    char* buffer = (char*)malloc((size_t)length + 1);
    if (!buffer)
    {
        return SQLITE_NOMEM;
    }

    memcpy(buffer, text, (size_t)length);
    buffer[length] = '\0';
    *out_value = buffer;
    return SQLITE_OK;
}

static int duplicate_optional_text_column(sqlite3_stmt* stmt, int column_index, char** out_value)
{
    if (!out_value)
    {
        return SQLITE_ERROR;
    }

    if (column_index < 0)
    {
        *out_value = NULL;
        return SQLITE_OK;
    }

    return duplicate_text_column(stmt, column_index, out_value);
}

static int find_column_index(sqlite3_stmt* stmt, const char* const* candidates, int candidate_count)
{
    if (!stmt || !candidates || candidate_count <= 0)
    {
        return -1;
    }

    int column_count = sqlite3_column_count(stmt);
    for (int i = 0; i < column_count; ++i)
    {
        const char* name = sqlite3_column_name(stmt, i);
        if (!name)
        {
            continue;
        }

        for (int j = 0; j < candidate_count; ++j)
        {
            const char* candidate = candidates[j];
            if (candidate && equals_ignore_case(name, candidate))
            {
                return i;
            }
        }
    }

    return -1;
}

static int get_int_column(sqlite3_stmt* stmt, int column_index)
{
    if (column_index < 0)
    {
        return 0;
    }

    if (sqlite3_column_type(stmt, column_index) == SQLITE_NULL)
    {
        return 0;
    }

    return sqlite3_column_int(stmt, column_index);
}

static long long get_int64_column(sqlite3_stmt* stmt, int column_index)
{
    if (column_index < 0)
    {
        return 0;
    }

    if (sqlite3_column_type(stmt, column_index) == SQLITE_NULL)
    {
        return 0;
    }

    return sqlite3_column_int64(stmt, column_index);
}

static double get_double_column(sqlite3_stmt* stmt, int column_index)
{
    if (column_index < 0)
    {
        return 0.0;
    }

    if (sqlite3_column_type(stmt, column_index) == SQLITE_NULL)
    {
        return 0.0;
    }

    return sqlite3_column_double(stmt, column_index);
}

SP_EXPORT int sp_usage_distribution_get(
    const char* db_path,
    const char* software,
    const char** keys,
    int key_count,
    sp_usage_distribution_record** records,
    int* record_count,
    char** error_message)
{
    if (records)
    {
        *records = NULL;
    }
    if (record_count)
    {
        *record_count = 0;
    }

    if (!keys || key_count <= 0)
    {
        return SQLITE_OK;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    rc = ensure_usage_distribution_schema(db, error_message);
    if (rc != SQLITE_OK)
    {
        sqlite3_close(db);
        return rc;
    }

    char* placeholders = NULL;
    rc = build_in_clause(key_count, &placeholders);
    if (rc != SQLITE_OK)
    {
        sqlite3_close(db);
        return rc;
    }

    const char* sql_template =
        "SELECT Whom, Payload, ResolvedTotal, UpdatedAt FROM UsageDistributionCache WHERE Software = ? AND Whom IN (%s);";
    size_t sql_length = strlen(sql_template) + strlen(placeholders) + 1;
    char* sql = (char*)malloc(sql_length);
    if (!sql)
    {
        free(placeholders);
        sqlite3_close(db);
        return SQLITE_NOMEM;
    }

    snprintf(sql, sql_length, sql_template, placeholders);
    free(placeholders);

    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    free(sql);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    rc = sqlite3_bind_text(stmt, 1, software ? software : "", -1, SQLITE_TRANSIENT);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        finalize_stmt(&stmt);
        sqlite3_close(db);
        return rc;
    }

    for (int i = 0; i < key_count; ++i)
    {
        rc = sqlite3_bind_text(stmt, i + 2, keys[i] ? keys[i] : "", -1, SQLITE_TRANSIENT);
        if (rc != SQLITE_OK)
        {
            set_sqlite_error(error_message, db);
            finalize_stmt(&stmt);
            sqlite3_close(db);
            return rc;
        }
    }

    sp_usage_distribution_record* result = NULL;
    int capacity = 0;
    int count = 0;

    while ((rc = sqlite3_step(stmt)) == SQLITE_ROW)
    {
        if (count >= capacity)
        {
            int new_capacity = capacity == 0 ? 8 : capacity * 2;
            sp_usage_distribution_record* resized = (sp_usage_distribution_record*)realloc(result, (size_t)new_capacity * sizeof(sp_usage_distribution_record));
            if (!resized)
            {
                rc = SQLITE_NOMEM;
                break;
            }
            result = resized;
            capacity = new_capacity;
        }

        sp_usage_distribution_record* record = &result[count];
        memset(record, 0, sizeof(*record));

        if (duplicate_text_column(stmt, 0, &record->whom) != SQLITE_OK ||
            duplicate_text_column(stmt, 1, &record->payload) != SQLITE_OK)
        {
            rc = SQLITE_NOMEM;
            break;
        }

        record->resolved_total = sqlite3_column_int64(stmt, 2);
        record->updated_at = sqlite3_column_int64(stmt, 3);
        ++count;
    }

    if (rc == SQLITE_DONE)
    {
        rc = SQLITE_OK;
    }

    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        if (result)
        {
            for (int i = 0; i < count; ++i)
            {
                free(result[i].whom);
                free(result[i].payload);
            }
            free(result);
        }
    }
    else
    {
        if (records)
        {
            *records = result;
        }
        else if (result)
        {
            for (int i = 0; i < count; ++i)
            {
                free(result[i].whom);
                free(result[i].payload);
            }
            free(result);
        }

        if (record_count)
        {
            *record_count = count;
        }
    }

    finalize_stmt(&stmt);
    sqlite3_close(db);
    return rc;
}

SP_EXPORT void sp_usage_distribution_free_records(
    sp_usage_distribution_record* records,
    int record_count)
{
    if (!records)
    {
        return;
    }

    for (int i = 0; i < record_count; ++i)
    {
        free(records[i].whom);
        free(records[i].payload);
    }

    free(records);
}

SP_EXPORT int sp_multi_software_get_all(
    const char* db_path,
    sp_multi_software_record** records,
    int* record_count,
    char** error_message)
{
    if (records)
    {
        *records = NULL;
    }
    if (record_count)
    {
        *record_count = 0;
    }

    if (!db_path)
    {
        set_error(error_message, "Invalid arguments", NULL);
        return SQLITE_ERROR;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    int exists = 0;
    rc = table_exists(db, "MultiSoftware", &exists);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    if (!exists)
    {
        sqlite3_close(db);
        return SQLITE_OK;
    }

    const char* sql = "SELECT SoftwareName, State, idc, Version FROM MultiSoftware";
    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    sp_multi_software_record* result = NULL;
    int capacity = 0;
    int count = 0;

    while ((rc = sqlite3_step(stmt)) == SQLITE_ROW)
    {
        if (count >= capacity)
        {
            int new_capacity = capacity == 0 ? 8 : capacity * 2;
            sp_multi_software_record* tmp = (sp_multi_software_record*)realloc(result, (size_t)new_capacity * sizeof(sp_multi_software_record));
            if (!tmp)
            {
                rc = SQLITE_NOMEM;
                break;
            }

            for (int i = capacity; i < new_capacity; ++i)
            {
                tmp[i].software_name = NULL;
                tmp[i].idc = NULL;
                tmp[i].state = 0;
                tmp[i].version = 0;
            }

            result = tmp;
            capacity = new_capacity;
        }

        sp_multi_software_record* current = &result[count];
        if (duplicate_text_column(stmt, 0, &current->software_name) != SQLITE_OK ||
            duplicate_text_column(stmt, 2, &current->idc) != SQLITE_OK)
        {
            rc = SQLITE_NOMEM;
            break;
        }

        current->state = sqlite3_column_int(stmt, 1);
        current->version = sqlite3_column_int(stmt, 3);
        ++count;
    }

    if (rc == SQLITE_DONE)
    {
        rc = SQLITE_OK;
    }

    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        if (result)
        {
            for (int i = 0; i < count; ++i)
            {
                free(result[i].software_name);
                free(result[i].idc);
            }
            free(result);
        }
    }
    else if (records)
    {
        *records = result;
        if (record_count)
        {
            *record_count = count;
        }
    }
    else if (result)
    {
        for (int i = 0; i < count; ++i)
        {
            free(result[i].software_name);
            free(result[i].idc);
        }
        free(result);
    }

    finalize_stmt(&stmt);
    sqlite3_close(db);
    return rc;
}

SP_EXPORT void sp_multi_software_free_records(
    sp_multi_software_record* records,
    int record_count)
{
    if (!records)
    {
        return;
    }

    for (int i = 0; i < record_count; ++i)
    {
        free(records[i].software_name);
        free(records[i].idc);
    }

    free(records);
}

static int bind_ip_location_record(sqlite3_stmt* stmt, const sp_ip_location_record* record, char** error_message)
{
    int rc = sqlite3_bind_text(stmt, 1, record->ip ? record->ip : "", -1, SQLITE_TRANSIENT);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, sqlite3_db_handle(stmt));
        return rc;
    }

    rc = sqlite3_bind_text(stmt, 2, record->province ? record->province : "", -1, SQLITE_TRANSIENT);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, sqlite3_db_handle(stmt));
        return rc;
    }

    rc = sqlite3_bind_text(stmt, 3, record->city ? record->city : "", -1, SQLITE_TRANSIENT);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, sqlite3_db_handle(stmt));
        return rc;
    }

    rc = sqlite3_bind_text(stmt, 4, record->district ? record->district : "", -1, SQLITE_TRANSIENT);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, sqlite3_db_handle(stmt));
        return rc;
    }

    rc = sqlite3_bind_int64(stmt, 5, record->updated_at);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, sqlite3_db_handle(stmt));
    }

    return rc;
}

SP_EXPORT int sp_ip_location_upsert(
    const char* db_path,
    const sp_ip_location_record* records,
    int record_count,
    char** error_message)
{
    if (!records || record_count <= 0)
    {
        return SQLITE_OK;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    rc = ensure_ip_location_schema(db, error_message);
    if (rc != SQLITE_OK)
    {
        sqlite3_close(db);
        return rc;
    }

    rc = begin_transaction(db, error_message);
    if (rc != SQLITE_OK)
    {
        sqlite3_close(db);
        return rc;
    }

    const char* sql =
        "INSERT INTO IpLocationCache (Ip, Province, City, District, UpdatedAt) "
        "VALUES (?, ?, ?, ?, ?) "
        "ON CONFLICT(Ip) DO UPDATE SET "
        "    Province = excluded.Province,"
        "    City = excluded.City,"
        "    District = excluded.District,"
        "    UpdatedAt = excluded.UpdatedAt;";

    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        goto cleanup;
    }

    for (int i = 0; i < record_count; ++i)
    {
        sqlite3_reset(stmt);
        sqlite3_clear_bindings(stmt);

        rc = bind_ip_location_record(stmt, &records[i], error_message);
        if (rc != SQLITE_OK)
        {
            goto cleanup;
        }

        rc = sqlite3_step(stmt);
        if (rc != SQLITE_DONE)
        {
            set_sqlite_error(error_message, db);
            goto cleanup;
        }
    }

    rc = commit_transaction(db, error_message);

cleanup:
    if (rc != SQLITE_OK)
    {
        rollback_transaction(db);
    }

    finalize_stmt(&stmt);
    sqlite3_close(db);
    return rc;
}

SP_EXPORT int sp_ip_location_get(
    const char* db_path,
    const char** ips,
    int ip_count,
    sp_ip_location_record** records,
    int* record_count,
    char** error_message)
{
    if (records)
    {
        *records = NULL;
    }
    if (record_count)
    {
        *record_count = 0;
    }

    if (!ips || ip_count <= 0)
    {
        return SQLITE_OK;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    rc = ensure_ip_location_schema(db, error_message);
    if (rc != SQLITE_OK)
    {
        sqlite3_close(db);
        return rc;
    }

    int max_variables = sqlite3_limit(db, SQLITE_LIMIT_VARIABLE_NUMBER, -1);
    if (max_variables <= 0)
    {
        max_variables = 999;
    }

    sp_ip_location_record* result = NULL;
    int capacity = 0;
    int count = 0;
    sqlite3_stmt* stmt = NULL;

    const char* sql_template =
        "SELECT Ip, Province, City, District, UpdatedAt FROM IpLocationCache WHERE Ip IN (%s);";

    for (int offset = 0; offset < ip_count; offset += max_variables)
    {
        int batch_size = ip_count - offset;
        if (batch_size > max_variables)
        {
            batch_size = max_variables;
        }

        char* placeholders = NULL;
        rc = build_in_clause(batch_size, &placeholders);
        if (rc != SQLITE_OK)
        {
            goto ip_cleanup;
        }

        size_t sql_length = strlen(sql_template) + strlen(placeholders) + 1;
        char* sql = (char*)malloc(sql_length);
        if (!sql)
        {
            free(placeholders);
            rc = SQLITE_NOMEM;
            goto ip_cleanup;
        }

        snprintf(sql, sql_length, sql_template, placeholders);
        free(placeholders);

        rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
        free(sql);
        if (rc != SQLITE_OK)
        {
            set_sqlite_error(error_message, db);
            goto ip_cleanup;
        }

        for (int i = 0; i < batch_size; ++i)
        {
            const char* value = ips[offset + i] ? ips[offset + i] : "";
            rc = sqlite3_bind_text(stmt, i + 1, value, -1, SQLITE_TRANSIENT);
            if (rc != SQLITE_OK)
            {
                set_sqlite_error(error_message, db);
                goto ip_cleanup;
            }
        }

        while ((rc = sqlite3_step(stmt)) == SQLITE_ROW)
        {
            if (count >= capacity)
            {
                int new_capacity = capacity == 0 ? 8 : capacity * 2;
                sp_ip_location_record* resized =
                    (sp_ip_location_record*)realloc(result, (size_t)new_capacity * sizeof(sp_ip_location_record));
                if (!resized)
                {
                    rc = SQLITE_NOMEM;
                    break;
                }

                result = resized;
                capacity = new_capacity;
            }

            sp_ip_location_record* record = &result[count];
            memset(record, 0, sizeof(*record));

            if (duplicate_text_column(stmt, 0, &record->ip) != SQLITE_OK ||
                duplicate_text_column(stmt, 1, &record->province) != SQLITE_OK ||
                duplicate_text_column(stmt, 2, &record->city) != SQLITE_OK ||
                duplicate_text_column(stmt, 3, &record->district) != SQLITE_OK)
            {
                rc = SQLITE_NOMEM;
                break;
            }

            record->updated_at = sqlite3_column_int64(stmt, 4);
            ++count;
        }

        if (rc == SQLITE_ROW)
        {
            rc = SQLITE_NOMEM;
        }
        else if (rc == SQLITE_DONE)
        {
            rc = SQLITE_OK;
        }

        finalize_stmt(&stmt);
        stmt = NULL;

        if (rc != SQLITE_OK)
        {
            set_sqlite_error(error_message, db);
            goto ip_cleanup;
        }
    }

    if (records)
    {
        *records = result;
    }
    else if (result)
    {
        for (int i = 0; i < count; ++i)
        {
            free(result[i].ip);
            free(result[i].province);
            free(result[i].city);
            free(result[i].district);
        }
        free(result);
        result = NULL;
    }

    if (record_count)
    {
        *record_count = count;
    }

    sqlite3_close(db);
    return SQLITE_OK;

ip_cleanup:
    finalize_stmt(&stmt);
    if (result)
    {
        for (int i = 0; i < count; ++i)
        {
            free(result[i].ip);
            free(result[i].province);
            free(result[i].city);
            free(result[i].district);
        }
        free(result);
    }

    sqlite3_close(db);
    return rc;
}

SP_EXPORT void sp_ip_location_free_records(
    sp_ip_location_record* records,
    int record_count)
{
    if (!records)
    {
        return;
    }

    for (int i = 0; i < record_count; ++i)
    {
        free(records[i].ip);
        free(records[i].province);
        free(records[i].city);
        free(records[i].district);
    }

    free(records);
}

SP_EXPORT int sp_blacklist_get_machines(
    const char* db_path,
    int has_type_filter,
    int type_value,
    sp_blacklist_machine_record** records,
    int* record_count,
    char** error_message)
{
    if (!db_path || !records || !record_count)
    {
        set_error(error_message, "Invalid arguments", NULL);
        return SQLITE_ERROR;
    }

    if (error_message)
    {
        *error_message = NULL;
    }

    *records = NULL;
    *record_count = 0;

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    rc = ensure_blacklist_schema(db, error_message);
    if (rc != SQLITE_OK)
    {
        sqlite3_close(db);
        return rc;
    }

    int exists = 0;
    rc = table_exists(db, "Blacklist", &exists);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    if (!exists)
    {
        sqlite3_close(db);
        return SQLITE_OK;
    }

    int has_type_column = 0;
    rc = table_has_column(db, "Blacklist", "Type", &has_type_column);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    const char* remark_column = NULL;
    const char* remark_candidates[] = { "Remarks", "Remark", "Memo" };
    int remark_candidate_count = (int)(sizeof(remark_candidates) / sizeof(remark_candidates[0]));
    for (int i = 0; i < remark_candidate_count && !remark_column; ++i)
    {
        int has_column = 0;
        rc = table_has_column(db, "Blacklist", remark_candidates[i], &has_column);
        if (rc != SQLITE_OK)
        {
            set_sqlite_error(error_message, db);
            sqlite3_close(db);
            return rc;
        }

        if (has_column)
        {
            remark_column = remark_candidates[i];
        }
    }

    const char* type_projection = has_type_column ? "Type" : "0 AS Type";
    char* remarks_projection = remark_column ? sqlite3_mprintf("%s AS Remarks", remark_column) : sqlite3_mprintf("'' AS Remarks");
    if (!remarks_projection)
    {
        sqlite3_close(db);
        set_error(error_message, "Out of memory while preparing blacklist query", db);
        return SQLITE_NOMEM;
    }

    const char* sql_template = (has_type_filter && has_type_column)
        ? "SELECT rowid AS RowId, Value, %s, %s FROM Blacklist WHERE Type = ? ORDER BY RowId DESC;"
        : "SELECT rowid AS RowId, Value, %s, %s FROM Blacklist ORDER BY RowId DESC;";
    char* sql = sqlite3_mprintf(sql_template, type_projection, remarks_projection);
    sqlite3_free(remarks_projection);
    if (!sql)
    {
        sqlite3_close(db);
        set_error(error_message, "Out of memory while preparing blacklist query", db);
        return SQLITE_NOMEM;
    }

    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    sqlite3_free(sql);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    if (has_type_filter && has_type_column)
    {
        rc = sqlite3_bind_int(stmt, 1, type_value);
        if (rc != SQLITE_OK)
        {
            set_sqlite_error(error_message, db);
            finalize_stmt(&stmt);
            sqlite3_close(db);
            return rc;
        }
    }

    sp_blacklist_machine_record* result = NULL;
    int count = 0;
    int capacity = 0;
    int step_rc = SQLITE_OK;

    while ((step_rc = sqlite3_step(stmt)) == SQLITE_ROW)
    {
        char* value_copy = NULL;
        int copy_rc = duplicate_text_column(stmt, 1, &value_copy);
        if (copy_rc == SQLITE_OK && value_copy)
        {
            trim_whitespace_in_place(value_copy);
        }

        if (copy_rc != SQLITE_OK)
        {
            if (copy_rc == SQLITE_NOMEM)
            {
                set_error(error_message, "Out of memory while reading blacklist entries", db);
            }
            rc = copy_rc;
            break;
        }

        if (!value_copy || value_copy[0] == '\0')
        {
            free(value_copy);
            value_copy = NULL;
            continue;
        }

        if (count == capacity)
        {
            int new_capacity = capacity == 0 ? 16 : capacity * 2;
            sp_blacklist_machine_record* resized = (sp_blacklist_machine_record*)realloc(result, (size_t)new_capacity * sizeof(sp_blacklist_machine_record));
            if (!resized)
            {
                free(value_copy);
                rc = SQLITE_NOMEM;
                set_error(error_message, "Out of memory while expanding blacklist results", db);
                break;
            }

            result = resized;
            capacity = new_capacity;
        }

        sp_blacklist_machine_record* record = &result[count];
        record->value = value_copy;
        record->type = sqlite3_column_int(stmt, 2);

        record->remarks = NULL;
        int remarks_rc = duplicate_text_column(stmt, 3, &record->remarks);
        if (remarks_rc == SQLITE_OK && record->remarks)
        {
            trim_whitespace_in_place(record->remarks);
            if (record->remarks[0] == '\0')
            {
                free(record->remarks);
                record->remarks = NULL;
            }
        }
        else if (remarks_rc == SQLITE_NOMEM)
        {
            free(record->value);
            record->value = NULL;
            rc = SQLITE_NOMEM;
            set_error(error_message, "Out of memory while reading blacklist remarks", db);
            break;
        }
        else if (remarks_rc != SQLITE_OK)
        {
            free(record->value);
            record->value = NULL;
            rc = remarks_rc;
            set_sqlite_error(error_message, db);
            break;
        }

        record->row_id = sqlite3_column_int64(stmt, 0);
        ++count;
    }

    if (rc == SQLITE_OK)
    {
        if (step_rc == SQLITE_DONE)
        {
            rc = SQLITE_OK;
        }
        else
        {
            rc = step_rc;
        }
    }

    if (rc != SQLITE_OK)
    {
        if (!error_message || !*error_message)
        {
            set_sqlite_error(error_message, db);
        }
        free_blacklist_machines(result, count);
        result = NULL;
        count = 0;
    }

    finalize_stmt(&stmt);
    sqlite3_close(db);

    if (rc == SQLITE_OK)
    {
        *records = result;
        *record_count = count;
    }

    return rc;
}

SP_EXPORT int sp_blacklist_add_machine(
    const char* db_path,
    const char* value,
    int type,
    const char* remarks,
    char** error_message)
{
    if (!db_path || !value)
    {
        set_error(error_message, "Invalid arguments", NULL);
        return SQLITE_ERROR;
    }

    if (error_message)
    {
        *error_message = NULL;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    rc = ensure_blacklist_schema(db, error_message);
    if (rc != SQLITE_OK)
    {
        sqlite3_close(db);
        return rc;
    }

    rc = begin_transaction(db, error_message);
    if (rc != SQLITE_OK)
    {
        sqlite3_close(db);
        return rc;
    }

    sqlite3_stmt* delete_stmt = NULL;
    rc = sqlite3_prepare_v2(db, "DELETE FROM Blacklist WHERE Value = ?;", -1, &delete_stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        rollback_transaction(db);
        sqlite3_close(db);
        return rc;
    }

    sqlite3_bind_text(delete_stmt, 1, value, -1, SQLITE_TRANSIENT);
    rc = sqlite3_step(delete_stmt);
    if (rc != SQLITE_DONE)
    {
        set_sqlite_error(error_message, db);
        finalize_stmt(&delete_stmt);
        rollback_transaction(db);
        sqlite3_close(db);
        return rc;
    }

    finalize_stmt(&delete_stmt);

    sqlite3_stmt* insert_stmt = NULL;
    rc = sqlite3_prepare_v2(db, "INSERT INTO Blacklist (Value, Type, Remarks) VALUES (?, ?, ?);", -1, &insert_stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        rollback_transaction(db);
        sqlite3_close(db);
        return rc;
    }

    sqlite3_bind_text(insert_stmt, 1, value, -1, SQLITE_TRANSIENT);
    sqlite3_bind_int(insert_stmt, 2, type);
    sqlite3_bind_text(insert_stmt, 3, remarks ? remarks : "", -1, SQLITE_TRANSIENT);

    rc = sqlite3_step(insert_stmt);
    if (rc != SQLITE_DONE)
    {
        set_sqlite_error(error_message, db);
        finalize_stmt(&insert_stmt);
        rollback_transaction(db);
        sqlite3_close(db);
        return rc;
    }

    finalize_stmt(&insert_stmt);

    rc = commit_transaction(db, error_message);
    if (rc != SQLITE_OK)
    {
        rollback_transaction(db);
        sqlite3_close(db);
        return rc;
    }

    sqlite3_close(db);
    return SQLITE_OK;
}

SP_EXPORT int sp_blacklist_delete_machines(
    const char* db_path,
    const char** values,
    int value_count,
    char** error_message)
{
    if (!db_path || !values || value_count <= 0)
    {
        return SQLITE_OK;
    }

    if (error_message)
    {
        *error_message = NULL;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    rc = ensure_blacklist_schema(db, error_message);
    if (rc != SQLITE_OK)
    {
        sqlite3_close(db);
        return rc;
    }

    rc = begin_transaction(db, error_message);
    if (rc != SQLITE_OK)
    {
        sqlite3_close(db);
        return rc;
    }

    size_t placeholder_length = (size_t)value_count * 2;
    char* placeholders = (char*)malloc(placeholder_length + 1);
    if (!placeholders)
    {
        rollback_transaction(db);
        sqlite3_close(db);
        set_error(error_message, "Out of memory", db);
        return SQLITE_NOMEM;
    }

    size_t offset = 0;
    for (int i = 0; i < value_count; ++i)
    {
        placeholders[offset++] = '?';
        if (i < value_count - 1)
        {
            placeholders[offset++] = ',';
        }
    }
    placeholders[offset] = '\0';

    char* sql = sqlite3_mprintf("DELETE FROM Blacklist WHERE Value IN (%s);", placeholders);
    free(placeholders);
    if (!sql)
    {
        rollback_transaction(db);
        sqlite3_close(db);
        set_error(error_message, "Out of memory", db);
        return SQLITE_NOMEM;
    }

    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    sqlite3_free(sql);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        rollback_transaction(db);
        sqlite3_close(db);
        return rc;
    }

    for (int i = 0; i < value_count; ++i)
    {
        sqlite3_bind_text(stmt, i + 1, values[i], -1, SQLITE_TRANSIENT);
    }

    rc = sqlite3_step(stmt);
    if (rc != SQLITE_DONE)
    {
        set_sqlite_error(error_message, db);
        finalize_stmt(&stmt);
        rollback_transaction(db);
        sqlite3_close(db);
        return rc;
    }

    finalize_stmt(&stmt);

    rc = commit_transaction(db, error_message);
    if (rc != SQLITE_OK)
    {
        rollback_transaction(db);
        sqlite3_close(db);
        return rc;
    }

    sqlite3_close(db);
    return SQLITE_OK;
}

SP_EXPORT void sp_blacklist_free_machines(
    sp_blacklist_machine_record* records,
    int record_count)
{
    free_blacklist_machines(records, record_count);
}

SP_EXPORT int sp_blacklist_get_logs(
    const char* db_path,
    int limit,
    sp_blacklist_log_record** records,
    int* record_count,
    char** error_message)
{
    if (!db_path || !records || !record_count)
    {
        set_error(error_message, "Invalid arguments", NULL);
        return SQLITE_ERROR;
    }

    if (error_message)
    {
        *error_message = NULL;
    }

    *records = NULL;
    *record_count = 0;

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    int exists = 0;
    rc = table_exists(db, "BlacklistLog", &exists);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    if (!exists)
    {
        sqlite3_close(db);
        return SQLITE_OK;
    }

    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, "SELECT rowid AS RowId, * FROM BlacklistLog ORDER BY RowId DESC LIMIT ?;", -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    sqlite3_bind_int(stmt, 1, limit > 0 ? limit : 0);

    int id_index = -1;
    int ip_index = -1;
    int card_index = -1;
    int pcsign_index = -1;
    int err_index = -1;
    int timestamp_index = -1;
    int rowid_index = -1;
    int column_count = sqlite3_column_count(stmt);

    for (int i = 0; i < column_count; ++i)
    {
        const char* name = sqlite3_column_name(stmt, i);
        if (!name)
        {
            continue;
        }

        if (id_index == -1 && (equals_ignore_case(name, "ID") || equals_ignore_case(name, "LogID") || equals_ignore_case(name, "Seq")))
        {
            id_index = i;
        }
        else if (ip_index == -1 && (equals_ignore_case(name, "IP") || equals_ignore_case(name, "ClientIP")))
        {
            ip_index = i;
        }
        else if (card_index == -1 && (equals_ignore_case(name, "Card") || equals_ignore_case(name, "CardNum") || equals_ignore_case(name, "CardNo")))
        {
            card_index = i;
        }
        else if (pcsign_index == -1 && (equals_ignore_case(name, "PCSign") || equals_ignore_case(name, "Machine") || equals_ignore_case(name, "PC") || equals_ignore_case(name, "Device")))
        {
            pcsign_index = i;
        }
        else if (err_index == -1 && (equals_ignore_case(name, "ErrEvents") || equals_ignore_case(name, "ErrEvent") || equals_ignore_case(name, "Event") || equals_ignore_case(name, "Reason")))
        {
            err_index = i;
        }
        else if (timestamp_index == -1 && (equals_ignore_case(name, "Timestamp") || equals_ignore_case(name, "TimeStamp") || equals_ignore_case(name, "AddTime") || equals_ignore_case(name, "CreateTime") || equals_ignore_case(name, "Time") || equals_ignore_case(name, "CreatedAt")))
        {
            timestamp_index = i;
        }
        else if (rowid_index == -1 && (equals_ignore_case(name, "RowId") || equals_ignore_case(name, "rowid")))
        {
            rowid_index = i;
        }
    }

    sp_blacklist_log_record* result = NULL;
    int count = 0;
    int capacity = 0;

    while ((rc = sqlite3_step(stmt)) == SQLITE_ROW)
    {
        if (count == capacity)
        {
            int new_capacity = capacity == 0 ? 16 : capacity * 2;
            sp_blacklist_log_record* resized = (sp_blacklist_log_record*)realloc(result, (size_t)new_capacity * sizeof(sp_blacklist_log_record));
            if (!resized)
            {
                rc = SQLITE_NOMEM;
                break;
            }

            result = resized;
            capacity = new_capacity;
        }

        sp_blacklist_log_record* record = &result[count];
        record->id = 0;
        record->timestamp = 0;
        record->row_id = rowid_index >= 0 ? sqlite3_column_int64(stmt, rowid_index) : 0;

        record->id = id_index >= 0 ? sqlite3_column_int64(stmt, id_index) : 0;
        if (record->id <= 0)
        {
            record->id = record->row_id;
        }

        record->timestamp = timestamp_index >= 0 ? sqlite3_column_int64(stmt, timestamp_index) : 0;
        if (record->timestamp <= 0)
        {
            record->timestamp = record->row_id;
        }

        record->ip = duplicate_trimmed_text(ip_index >= 0 ? sqlite3_column_text(stmt, ip_index) : NULL);
        if (!record->ip)
        {
            rc = SQLITE_NOMEM;
            break;
        }

        record->card = duplicate_trimmed_text(card_index >= 0 ? sqlite3_column_text(stmt, card_index) : NULL);
        if (!record->card)
        {
            free(record->ip);
            rc = SQLITE_NOMEM;
            break;
        }

        record->pcsign = duplicate_trimmed_text(pcsign_index >= 0 ? sqlite3_column_text(stmt, pcsign_index) : NULL);
        if (!record->pcsign)
        {
            free(record->ip);
            free(record->card);
            rc = SQLITE_NOMEM;
            break;
        }

        record->err_events = duplicate_trimmed_text(err_index >= 0 ? sqlite3_column_text(stmt, err_index) : NULL);
        if (!record->err_events)
        {
            free(record->ip);
            free(record->card);
            free(record->pcsign);
            rc = SQLITE_NOMEM;
            break;
        }

        ++count;
    }

    if (rc == SQLITE_DONE)
    {
        rc = SQLITE_OK;
    }

    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        free_blacklist_logs(result, count);
        result = NULL;
        count = 0;
    }

    finalize_stmt(&stmt);
    sqlite3_close(db);

    if (rc == SQLITE_OK)
    {
        *records = result;
        *record_count = count;
    }

    return rc;
}

SP_EXPORT void sp_blacklist_free_logs(
    sp_blacklist_log_record* records,
    int record_count)
{
    free_blacklist_logs(records, record_count);
}

// 本地查询全部代理信息，并构建本地结构体数组。
SP_EXPORT int sp_agent_get_all(
    const char* db_path,
    sp_agent_record** records,
    int* record_count,
    char** error_message)
{
    SP_VMP_SECTION("sp_agent_get_all");
    if (!db_path || !records || !record_count)
    {
        set_error(error_message, "Invalid arguments", NULL);
        return SQLITE_ERROR;
    }

    *records = NULL;
    *record_count = 0;

    static const char* kAgentSelectSql =
        "SELECT User, Password, AccountBalance, AccountTime, Duration, Authority, "
        "CardTypeAuthName, CardsEnable, Remarks, FNode, Stat, deltm, Duration_, "
        "Parities, TatalParities FROM Agents ORDER BY User ASC;";

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, kAgentSelectSql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    std::vector<sp_agent_record> temp_records;
    while ((rc = sqlite3_step(stmt)) == SQLITE_ROW)
    {
        sp_agent_record record;
        int populate_rc = populate_agent_record(stmt, &record);
        if (populate_rc != SQLITE_OK)
        {
            free_agent_record(&record);
            for (auto& existing : temp_records)
            {
                free_agent_record(&existing);
            }
            finalize_stmt(&stmt);
            sqlite3_close(db);
            set_error(error_message, "Out of memory", NULL);
            return populate_rc;
        }
        temp_records.push_back(record);
    }

    if (rc != SQLITE_DONE)
    {
        for (auto& existing : temp_records)
        {
            free_agent_record(&existing);
        }
        finalize_stmt(&stmt);
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return SQLITE_ERROR;
    }

    finalize_stmt(&stmt);
    sqlite3_close(db);

    if (temp_records.empty())
    {
        return SQLITE_OK;
    }

    sp_agent_record* result = static_cast<sp_agent_record*>(std::calloc(temp_records.size(), sizeof(sp_agent_record)));
    if (!result)
    {
        for (auto& existing : temp_records)
        {
            free_agent_record(&existing);
        }
        set_error(error_message, "Out of memory", NULL);
        return SQLITE_NOMEM;
    }

    for (std::size_t i = 0; i < temp_records.size(); ++i)
    {
        result[i] = temp_records[i];
        std::memset(&temp_records[i], 0, sizeof(sp_agent_record));
    }

    *records = result;
    *record_count = static_cast<int>(temp_records.size());
    return SQLITE_OK;
}

// 根据用户名从云端查询单个代理信息。
SP_EXPORT int sp_agent_get_by_username(
    const char* db_path,
    const char* username,
    sp_agent_record** record,
    int* has_value,
    char** error_message)
{
    SP_VMP_SECTION("sp_agent_get_by_username");
    if (!db_path || !username || !record || !has_value)
    {
        set_error(error_message, "Invalid arguments", NULL);
        return SQLITE_ERROR;
    }

    *record = NULL;
    *has_value = 0;

    std::vector<std::uint8_t> payload;
    {
        sp::BinaryWriter writer(payload);
        writer.write_string(db_path);
        writer.write_string(username);
    }

    std::vector<std::uint8_t> response;
    if (!send_cloud_request(sp::CloudQuery::AgentGetByUsername, payload, response, error_message))
    {
        return SQLITE_ERROR;
    }

    cloud_sql_instruction instruction;
    int rc = parse_cloud_sql_instruction(response, &instruction, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    if (instruction.sql.empty())
    {
        return SQLITE_OK;
    }

    sqlite3* db = NULL;
    rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, instruction.sql.c_str(), -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    rc = sqlite3_step(stmt);
    if (rc == SQLITE_ROW)
    {
        sp_agent_record* result = static_cast<sp_agent_record*>(std::calloc(1, sizeof(sp_agent_record)));
        if (!result)
        {
            finalize_stmt(&stmt);
            sqlite3_close(db);
            set_error(error_message, "Out of memory", NULL);
            return SQLITE_NOMEM;
        }

        int populate_rc = populate_agent_record(stmt, result);
        if (populate_rc != SQLITE_OK)
        {
            finalize_stmt(&stmt);
            sqlite3_close(db);
            free_agent_record(result);
            free(result);
            set_error(error_message, "Out of memory", NULL);
            return populate_rc;
        }

        *record = result;
        *has_value = 1;
        finalize_stmt(&stmt);
        sqlite3_close(db);
        return SQLITE_OK;
    }
    else if (rc == SQLITE_DONE)
    {
        finalize_stmt(&stmt);
        sqlite3_close(db);
        return SQLITE_OK;
    }
    else
    {
        finalize_stmt(&stmt);
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return SQLITE_ERROR;
    }
}

SP_EXPORT void sp_agent_free_records(
    sp_agent_record* records,
    int record_count)
{
    free_agent_records(records, record_count);
}

// 批量更新代理的启用状态，在本地执行 SQL 以避免云端延迟。
SP_EXPORT int sp_agent_set_status(
    const char* db_path,
    const char** usernames,
    int username_count,
    int enable,
    char** error_message)
{
    SP_VMP_SECTION("sp_agent_set_status");

    if (!db_path || (!usernames && username_count > 0))
    {
        set_error(error_message, "Invalid arguments", NULL);
        return SQLITE_ERROR;
    }

    if (username_count <= 0)
    {
        return SQLITE_OK;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    int stat_value = enable ? 0 : 1;
    int cards_enable = enable ? 1 : 0;

    std::string sql = "UPDATE Agents SET Stat = ?, CardsEnable = ? WHERE lower(User) IN (";
    for (int i = 0; i < username_count; ++i)
    {
        if (i > 0)
        {
            sql += ", ";
        }
        sql += "lower(?)";
    }

    sql += ");";

    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, sql.c_str(), -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    rc = sqlite3_bind_int(stmt, 1, stat_value);
    if (rc == SQLITE_OK)
    {
        rc = sqlite3_bind_int(stmt, 2, cards_enable);
    }
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        finalize_stmt(&stmt);
        sqlite3_close(db);
        return rc;
    }

    for (int i = 0; i < username_count; ++i)
    {
        const char* name = usernames[i] ? usernames[i] : "";
        rc = sqlite3_bind_text(stmt, 3 + i, name, -1, SQLITE_TRANSIENT);
        if (rc != SQLITE_OK)
        {
            set_sqlite_error(error_message, db);
            finalize_stmt(&stmt);
            sqlite3_close(db);
            return rc;
        }
    }

    rc = sqlite3_step(stmt);
    if (rc != SQLITE_DONE)
    {
        set_sqlite_error(error_message, db);
        finalize_stmt(&stmt);
        sqlite3_close(db);
        return rc;
    }

    finalize_stmt(&stmt);

    sqlite3_close(db);
    return SQLITE_OK;
}

// 在本地更新指定代理的备注信息，减少对云端的依赖。
SP_EXPORT int sp_agent_update_remark(
    const char* db_path,
    const char* username,
    const char* remark,
    char** error_message)
{
    SP_VMP_SECTION("sp_agent_update_remark");
    if (!db_path || !username)
    {
        set_error(error_message, "Invalid arguments", NULL);
        return SQLITE_ERROR;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    const char* sql = "UPDATE Agents SET Remarks = ? WHERE lower(User) = lower(?);";

    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    const char* remark_value = remark ? remark : "";
    rc = sqlite3_bind_text(stmt, 1, remark_value, -1, SQLITE_TRANSIENT);
    if (rc == SQLITE_OK)
    {
        rc = sqlite3_bind_text(stmt, 2, username, -1, SQLITE_TRANSIENT);
    }

    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        finalize_stmt(&stmt);
        sqlite3_close(db);
        return rc;
    }

    rc = sqlite3_step(stmt);
    if (rc != SQLITE_DONE)
    {
        set_sqlite_error(error_message, db);
        finalize_stmt(&stmt);
        sqlite3_close(db);
        return rc;
    }

    finalize_stmt(&stmt);

    sqlite3_close(db);
    return SQLITE_OK;
}

SP_EXPORT int sp_agent_create(
    const char* db_path,
    const char* username,
    const char* password,
    double balance,
    long long time_stock,
    const char* authority,
    const char* card_types,
    const char* remark,
    const char* fnode,
    double parities,
    double total_parities,
    char** error_message)
{
    if (!db_path || !username || !password)
    {
        set_error(error_message, "Invalid arguments", NULL);
        return SQLITE_ERROR;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    int exists = 0;
    rc = table_exists(db, "Agents", &exists);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    if (!exists)
    {
        const char* create_sql =
            "CREATE TABLE IF NOT EXISTS Agents ("
            "User TEXT PRIMARY KEY,"
            "Password TEXT NOT NULL,"
            "AccountBalance REAL NOT NULL DEFAULT 0,"
            "AccountTime INTEGER NOT NULL DEFAULT 0,"
            "Duration TEXT,"
            "Authority TEXT,"
            "CardTypeAuthName TEXT,"
            "CardsEnable INTEGER NOT NULL DEFAULT 1,"
            "Remarks TEXT,"
            "FNode TEXT,"
            "Stat INTEGER NOT NULL DEFAULT 0,"
            "deltm INTEGER NOT NULL DEFAULT 0,"
            "Duration_ INTEGER NOT NULL DEFAULT 0,"
            "Parities REAL NOT NULL DEFAULT 100,"
            "TatalParities REAL NOT NULL DEFAULT 100"
            ");";

        char* errmsg = NULL;
        rc = sqlite3_exec(db, create_sql, NULL, NULL, &errmsg);
        if (rc != SQLITE_OK)
        {
            if (errmsg)
            {
                set_error(error_message, errmsg, db);
                sqlite3_free(errmsg);
            }
            else
            {
                set_sqlite_error(error_message, db);
            }

            sqlite3_close(db);
            return rc;
        }
    }

    const char* sql =
        "INSERT INTO Agents "
        "(User, Password, AccountBalance, AccountTime, Duration, Authority, CardTypeAuthName, CardsEnable, Remarks, FNode, Stat, "
        "deltm, Duration_, Parities, TatalParities) "
        "VALUES (?, ?, ?, ?, '', ?, ?, 1, ?, ?, 0, 0, 0, ?, ?);";

    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    sqlite3_bind_text(stmt, 1, username, -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 2, password, -1, SQLITE_TRANSIENT);
    sqlite3_bind_double(stmt, 3, balance);
    sqlite3_bind_int64(stmt, 4, time_stock);
    sqlite3_bind_text(stmt, 5, authority ? authority : "", -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 6, card_types ? card_types : "", -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 7, remark ? remark : "", -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 8, fnode ? fnode : "", -1, SQLITE_TRANSIENT);
    sqlite3_bind_double(stmt, 9, parities);
    sqlite3_bind_double(stmt, 10, total_parities);

    rc = sqlite3_step(stmt);
    if (rc != SQLITE_DONE)
    {
        set_sqlite_error(error_message, db);
        rc = SQLITE_ERROR;
    }
    else
    {
        rc = SQLITE_OK;
    }

    finalize_stmt(&stmt);
    sqlite3_close(db);
    return rc;
}

SP_EXPORT int sp_agent_soft_delete(
    const char* db_path,
    const char** usernames,
    int username_count,
    char** error_message)
{
    if (!db_path || (!usernames && username_count > 0))
    {
        set_error(error_message, "Invalid arguments", NULL);
        return SQLITE_ERROR;
    }

    if (username_count <= 0)
    {
        return SQLITE_OK;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    int exists = 0;
    rc = table_exists(db, "Agents", &exists);
    if (rc != SQLITE_OK || !exists)
    {
        if (rc != SQLITE_OK)
        {
            set_sqlite_error(error_message, db);
        }

        sqlite3_close(db);
        return rc == SQLITE_OK ? SQLITE_OK : rc;
    }

    rc = begin_transaction(db, error_message);
    if (rc != SQLITE_OK)
    {
        sqlite3_close(db);
        return rc;
    }

    const char* sql = "UPDATE Agents SET deltm = ? WHERE User = ?;";
    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        rollback_transaction(db);
        sqlite3_close(db);
        return rc;
    }

    long long timestamp = (long long)time(NULL);

    for (int i = 0; i < username_count; i++)
    {
        const char* name = usernames[i];
        if (!name)
        {
            continue;
        }

        sqlite3_reset(stmt);
        sqlite3_clear_bindings(stmt);
        sqlite3_bind_int64(stmt, 1, timestamp);
        sqlite3_bind_text(stmt, 2, name, -1, SQLITE_TRANSIENT);

        rc = sqlite3_step(stmt);
        if (rc != SQLITE_DONE)
        {
            set_sqlite_error(error_message, db);
            rc = SQLITE_ERROR;
            break;
        }
    }

    finalize_stmt(&stmt);

    if (rc == SQLITE_OK)
    {
        rc = commit_transaction(db, error_message);
    }
    else
    {
        rollback_transaction(db);
    }

    sqlite3_close(db);
    return rc;
}

SP_EXPORT int sp_agent_update_password(
    const char* db_path,
    const char* username,
    const char* password,
    char** error_message)
{
    if (!db_path || !username || !password)
    {
        set_error(error_message, "Invalid arguments", NULL);
        return SQLITE_ERROR;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    int exists = 0;
    rc = table_exists(db, "Agents", &exists);
    if (rc != SQLITE_OK || !exists)
    {
        if (rc != SQLITE_OK)
        {
            set_sqlite_error(error_message, db);
        }

        sqlite3_close(db);
        return rc == SQLITE_OK ? SQLITE_OK : rc;
    }

    const char* sql = "UPDATE Agents SET Password = ? WHERE User = ?;";
    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    sqlite3_bind_text(stmt, 1, password, -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 2, username, -1, SQLITE_TRANSIENT);

    rc = sqlite3_step(stmt);
    if (rc != SQLITE_DONE)
    {
        set_sqlite_error(error_message, db);
        rc = SQLITE_ERROR;
    }
    else
    {
        rc = SQLITE_OK;
    }

    finalize_stmt(&stmt);
    sqlite3_close(db);
    return rc;
}

SP_EXPORT int sp_agent_add_balance(
    const char* db_path,
    const char* username,
    double balance,
    long long time_stock,
    char** error_message)
{
    if (!db_path || !username)
    {
        set_error(error_message, "Invalid arguments", NULL);
        return SQLITE_ERROR;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    int exists = 0;
    rc = table_exists(db, "Agents", &exists);
    if (rc != SQLITE_OK || !exists)
    {
        if (rc != SQLITE_OK)
        {
            set_sqlite_error(error_message, db);
        }

        sqlite3_close(db);
        return rc == SQLITE_OK ? SQLITE_OK : rc;
    }

    const char* sql =
        "UPDATE Agents SET AccountBalance = AccountBalance + ?, AccountTime = AccountTime + ? WHERE User = ?;";

    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    sqlite3_bind_double(stmt, 1, balance);
    sqlite3_bind_int64(stmt, 2, time_stock);
    sqlite3_bind_text(stmt, 3, username, -1, SQLITE_TRANSIENT);

    rc = sqlite3_step(stmt);
    if (rc != SQLITE_DONE)
    {
        set_sqlite_error(error_message, db);
        rc = SQLITE_ERROR;
    }
    else
    {
        rc = SQLITE_OK;
    }

    finalize_stmt(&stmt);
    sqlite3_close(db);
    return rc;
}

SP_EXPORT int sp_agent_set_card_types(
    const char* db_path,
    const char* username,
    const char* card_types,
    char** error_message)
{
    if (!db_path || !username)
    {
        set_error(error_message, "Invalid arguments", NULL);
        return SQLITE_ERROR;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    int exists = 0;
    rc = table_exists(db, "Agents", &exists);
    if (rc != SQLITE_OK || !exists)
    {
        if (rc != SQLITE_OK)
        {
            set_sqlite_error(error_message, db);
        }

        sqlite3_close(db);
        return rc == SQLITE_OK ? SQLITE_OK : rc;
    }

    const char* sql = "UPDATE Agents SET CardTypeAuthName = ? WHERE User = ?;";
    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    sqlite3_bind_text(stmt, 1, card_types ? card_types : "", -1, SQLITE_TRANSIENT);
    sqlite3_bind_text(stmt, 2, username, -1, SQLITE_TRANSIENT);

    rc = sqlite3_step(stmt);
    if (rc != SQLITE_DONE)
    {
        set_sqlite_error(error_message, db);
        rc = SQLITE_ERROR;
    }
    else
    {
        rc = SQLITE_OK;
    }

    finalize_stmt(&stmt);
    sqlite3_close(db);
    return rc;
}

SP_EXPORT int sp_agent_get_statistics(
    const char* db_path,
    const char* username,
    sp_agent_statistics* statistics,
    char** error_message)
{
    if (!db_path || !username || !statistics)
    {
        set_error(error_message, "Invalid arguments", NULL);
        return SQLITE_ERROR;
    }

    memset(statistics, 0, sizeof(*statistics));

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    int agents_exists = 0;
    rc = table_exists(db, "Agents", &agents_exists);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    int cards_exists = 0;
    rc = table_exists(db, "CardInfo", &cards_exists);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    if (!cards_exists)
    {
        cards_exists = 0;
    }

    const char* count_total_sql = "SELECT COUNT(*) FROM CardInfo WHERE Whom = ?;";
    const char* count_active_sql =
        "SELECT COUNT(*) FROM CardInfo WHERE Whom = ? AND (state IS NULL OR state = '' OR state = '0' OR state = 'active' OR state = '启用') AND delstate = 0;";
    const char* count_used_sql = "SELECT COUNT(*) FROM CardInfo WHERE Whom = ? AND LoginCount > 0;";
    const char* count_expired_sql =
        "SELECT COUNT(*) FROM CardInfo WHERE Whom = ? AND ExpiredTime__ > 0 AND ExpiredTime__ < strftime('%s','now');";
    const char* count_agents_sql = "SELECT COUNT(*) FROM Agents WHERE deltm = 0 AND FNode LIKE ?;";

    if (cards_exists)
    {
        sqlite3_stmt* stmt = NULL;

        rc = sqlite3_prepare_v2(db, count_total_sql, -1, &stmt, NULL);
        if (rc != SQLITE_OK)
        {
            set_sqlite_error(error_message, db);
            finalize_stmt(&stmt);
            sqlite3_close(db);
            return rc;
        }
        sqlite3_bind_text(stmt, 1, username, -1, SQLITE_TRANSIENT);
        if (sqlite3_step(stmt) == SQLITE_ROW)
        {
            statistics->total_cards = sqlite3_column_int64(stmt, 0);
        }
        finalize_stmt(&stmt);

        rc = sqlite3_prepare_v2(db, count_active_sql, -1, &stmt, NULL);
        if (rc != SQLITE_OK)
        {
            set_sqlite_error(error_message, db);
            finalize_stmt(&stmt);
            sqlite3_close(db);
            return rc;
        }
        sqlite3_bind_text(stmt, 1, username, -1, SQLITE_TRANSIENT);
        if (sqlite3_step(stmt) == SQLITE_ROW)
        {
            statistics->active_cards = sqlite3_column_int64(stmt, 0);
        }
        finalize_stmt(&stmt);

        rc = sqlite3_prepare_v2(db, count_used_sql, -1, &stmt, NULL);
        if (rc != SQLITE_OK)
        {
            set_sqlite_error(error_message, db);
            finalize_stmt(&stmt);
            sqlite3_close(db);
            return rc;
        }
        sqlite3_bind_text(stmt, 1, username, -1, SQLITE_TRANSIENT);
        if (sqlite3_step(stmt) == SQLITE_ROW)
        {
            statistics->used_cards = sqlite3_column_int64(stmt, 0);
        }
        finalize_stmt(&stmt);

        rc = sqlite3_prepare_v2(db, count_expired_sql, -1, &stmt, NULL);
        if (rc != SQLITE_OK)
        {
            set_sqlite_error(error_message, db);
            finalize_stmt(&stmt);
            sqlite3_close(db);
            return rc;
        }
        sqlite3_bind_text(stmt, 1, username, -1, SQLITE_TRANSIENT);
        if (sqlite3_step(stmt) == SQLITE_ROW)
        {
            statistics->expired_cards = sqlite3_column_int64(stmt, 0);
        }
        finalize_stmt(&stmt);
    }

    if (agents_exists)
    {
        sqlite3_stmt* stmt = NULL;
        rc = sqlite3_prepare_v2(db, count_agents_sql, -1, &stmt, NULL);
        if (rc != SQLITE_OK)
        {
            set_sqlite_error(error_message, db);
            finalize_stmt(&stmt);
            sqlite3_close(db);
            return rc;
        }

        char pattern[512];
        snprintf(pattern, sizeof(pattern), "%%[%s]%%", username);
        sqlite3_bind_text(stmt, 1, pattern, -1, SQLITE_TRANSIENT);
        if (sqlite3_step(stmt) == SQLITE_ROW)
        {
            statistics->sub_agents = sqlite3_column_int64(stmt, 0);
        }
        finalize_stmt(&stmt);
    }

    sqlite3_close(db);
    return SQLITE_OK;
}

static void free_card_creator_records(sp_card_creator_record* records, int record_count)
{
    if (!records)
    {
        return;
    }

    for (int i = 0; i < record_count; ++i)
    {
        free(records[i].whom);
        records[i].whom = NULL;
    }

    free(records);
}

static void free_card_ip_records(sp_card_ip_record* records, int record_count)
{
    if (!records)
    {
        return;
    }

    for (int i = 0; i < record_count; ++i)
    {
        free(records[i].value);
        records[i].value = NULL;
    }

    free(records);
}

SP_EXPORT int sp_card_get_creators(
    const char* db_path,
    sp_card_creator_record** records,
    int* record_count,
    char** error_message)
{
    if (records)
    {
        *records = NULL;
    }
    if (record_count)
    {
        *record_count = 0;
    }

    if (!db_path)
    {
        set_error(error_message, "Invalid database path", NULL);
        return SQLITE_ERROR;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    int exists = 0;
    rc = table_exists(db, "CardInfo", &exists);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    if (!exists)
    {
        sqlite3_close(db);
        return SQLITE_OK;
    }

    const char* sql =
        "SELECT DISTINCT Whom FROM CardInfo "
        "WHERE Whom IS NOT NULL AND TRIM(Whom) <> '' "
        "ORDER BY Whom COLLATE NOCASE;";

    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    sp_card_creator_record* result = NULL;
    int capacity = 0;
    int count = 0;

    while ((rc = sqlite3_step(stmt)) == SQLITE_ROW)
    {
        if (count >= capacity)
        {
            int new_capacity = capacity == 0 ? 8 : capacity * 2;
            sp_card_creator_record* resized = (sp_card_creator_record*)realloc(result, (size_t)new_capacity * sizeof(sp_card_creator_record));
            if (!resized)
            {
                rc = SQLITE_NOMEM;
                break;
            }
            result = resized;
            capacity = new_capacity;
        }

        sp_card_creator_record* record = &result[count];
        memset(record, 0, sizeof(*record));

        if (duplicate_text_column(stmt, 0, &record->whom) != SQLITE_OK)
        {
            rc = SQLITE_NOMEM;
            break;
        }

        count++;
    }

    if (rc == SQLITE_DONE)
    {
        rc = SQLITE_OK;
    }
    else if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
    }

    finalize_stmt(&stmt);
    sqlite3_close(db);

    if (rc != SQLITE_OK)
    {
        free_card_creator_records(result, count);
        return rc;
    }

    if (records)
    {
        *records = result;
    }
    else
    {
        free_card_creator_records(result, count);
    }

    if (record_count)
    {
        *record_count = count;
    }

    return SQLITE_OK;
}

SP_EXPORT void sp_card_free_creators(
    sp_card_creator_record* records,
    int record_count)
{
    free_card_creator_records(records, record_count);
}

SP_EXPORT int sp_card_get_ips(
    const char* db_path,
    const char** creators,
    int creator_count,
    sp_card_ip_record** records,
    int* record_count,
    char** error_message)
{
    if (records)
    {
        *records = NULL;
    }
    if (record_count)
    {
        *record_count = 0;
    }

    if (!db_path)
    {
        set_error(error_message, "Invalid database path", NULL);
        return SQLITE_ERROR;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    int exists = 0;
    rc = table_exists(db, "CardInfo", &exists);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    if (!exists)
    {
        sqlite3_close(db);
        return SQLITE_OK;
    }

    char* placeholders = NULL;
    char* sql = NULL;
    const char* sql_base =
        "SELECT IP FROM CardInfo WHERE delstate = 0 AND state = '启用' AND IP <> ''";

    if (creator_count > 0)
    {
        rc = build_in_clause(creator_count, &placeholders);
        if (rc != SQLITE_OK)
        {
            sqlite3_close(db);
            return rc;
        }

        const char* sql_template = "%s AND Whom IN (%s);";
        size_t sql_length = strlen(sql_template) + strlen(sql_base) + strlen(placeholders) + 1;
        sql = (char*)malloc(sql_length);
        if (!sql)
        {
            free(placeholders);
            sqlite3_close(db);
            return SQLITE_NOMEM;
        }

        snprintf(sql, sql_length, sql_template, sql_base, placeholders);
        free(placeholders);
    }
    else
    {
        size_t sql_length = strlen(sql_base) + 2;
        sql = (char*)malloc(sql_length);
        if (!sql)
        {
            sqlite3_close(db);
            return SQLITE_NOMEM;
        }

        snprintf(sql, sql_length, "%s;", sql_base);
    }

    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    free(sql);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    for (int i = 0; i < creator_count; ++i)
    {
        rc = sqlite3_bind_text(stmt, i + 1, creators[i] ? creators[i] : "", -1, SQLITE_TRANSIENT);
        if (rc != SQLITE_OK)
        {
            set_sqlite_error(error_message, db);
            finalize_stmt(&stmt);
            sqlite3_close(db);
            return rc;
        }
    }

    sp_card_ip_record* result = NULL;
    int capacity = 0;
    int count = 0;

    while ((rc = sqlite3_step(stmt)) == SQLITE_ROW)
    {
        if (count >= capacity)
        {
            int new_capacity = capacity == 0 ? 16 : capacity * 2;
            sp_card_ip_record* resized = (sp_card_ip_record*)realloc(result, (size_t)new_capacity * sizeof(sp_card_ip_record));
            if (!resized)
            {
                rc = SQLITE_NOMEM;
                break;
            }
            result = resized;
            capacity = new_capacity;
        }

        sp_card_ip_record* record = &result[count];
        memset(record, 0, sizeof(*record));

        if (duplicate_text_column(stmt, 0, &record->value) != SQLITE_OK)
        {
            rc = SQLITE_NOMEM;
            break;
        }

        count++;
    }

    if (rc == SQLITE_DONE)
    {
        rc = SQLITE_OK;
    }
    else if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
    }

    finalize_stmt(&stmt);
    sqlite3_close(db);

    if (rc != SQLITE_OK)
    {
        free_card_ip_records(result, count);
        return rc;
    }

    if (records)
    {
        *records = result;
    }
    else
    {
        free_card_ip_records(result, count);
    }

    if (record_count)
    {
        *record_count = count;
    }

    return SQLITE_OK;
}

SP_EXPORT void sp_card_free_ips(
    sp_card_ip_record* records,
    int record_count)
{
    free_card_ip_records(records, record_count);
}

static void free_card_type_record_internal(sp_card_type_record* record)
{
    if (!record)
    {
        return;
    }

    free(record->name);
    free(record->prefix);
    free(record->param);
    free(record->remarks);
    record->name = NULL;
    record->prefix = NULL;
    record->param = NULL;
    record->remarks = NULL;
}

static int populate_card_type_record(sqlite3_stmt* stmt, sp_card_type_record* record)
{
    if (!record)
    {
        return SQLITE_ERROR;
    }

    memset(record, 0, sizeof(*record));

    const char* name_candidates[] = { "Name" };
    const char* prefix_candidates[] = { "Prefix" };
    const char* param_candidates[] = { "Param" };
    const char* remarks_candidates[] = { "Remarks", "Remark" };
    const char* duration_candidates[] = { "Duration" };
    const char* fyi_candidates[] = { "FYI", "Fyi" };
    const char* price_candidates[] = { "Price" };
    const char* bind_candidates[] = { "Bind" };
    const char* open_num_candidates[] = { "OpenNum" };
    const char* cannot_change_candidates[] = { "CannotBeChanged", "CannotChanged" };
    const char* unbind_limit_candidates[] = { "Attr_UnBindLimitTime", "Attr_UnbindLimitTime", "Attr_UnBindLimit", "Attr_UnbindLimit" };
    const char* unbind_deduct_candidates[] = { "Attr_UnBindDeductTime", "Attr_UnbindDeductTime" };
    const char* unbind_free_candidates[] = { "Attr_UnBindFreeCount", "Attr_UnbindFreeCount" };
    const char* unbind_max_candidates[] = { "Attr_UnBindMaxCount", "Attr_UnbindMaxCount" };
    const char* bind_ip_candidates[] = { "BindIP", "BindIp" };
    const char* bind_machine_candidates[] = { "BindMachineNum", "BindMachineCount" };
    const char* lock_bind_candidates[] = { "LockBindPcsign", "LockBindPcSign", "LockBindMachine" };
    const char* activate_candidates[] = { "ActivateTime_", "ActivateTime" };
    const char* expired_candidates[] = { "ExpiredTime_", "ExpiredTime" };
    const char* last_login_candidates[] = { "LastLoginTime_", "LastLoginTime" };
    const char* delstate_candidates[] = { "delstate", "DelState" };
    const char* city_candidates[] = { "cty", "City" };
    const char* expired2_candidates[] = { "ExpiredTime__", "ExpiredTime2", "ExpiredTimeBackup" };

    if (duplicate_optional_text_column(stmt, find_column_index(stmt, name_candidates, 1), &record->name) != SQLITE_OK ||
        duplicate_optional_text_column(stmt, find_column_index(stmt, prefix_candidates, 1), &record->prefix) != SQLITE_OK ||
        duplicate_optional_text_column(stmt, find_column_index(stmt, param_candidates, 1), &record->param) != SQLITE_OK ||
        duplicate_optional_text_column(stmt, find_column_index(stmt, remarks_candidates, 2), &record->remarks) != SQLITE_OK)
    {
        free_card_type_record_internal(record);
        return SQLITE_NOMEM;
    }

    record->duration = get_int_column(stmt, find_column_index(stmt, duration_candidates, 1));
    record->fyi = get_int_column(stmt, find_column_index(stmt, fyi_candidates, 2));
    record->price = get_double_column(stmt, find_column_index(stmt, price_candidates, 1));
    record->bind = get_int_column(stmt, find_column_index(stmt, bind_candidates, 1));
    record->open_num = get_int_column(stmt, find_column_index(stmt, open_num_candidates, 1));
    record->cannot_be_changed = get_int_column(stmt, find_column_index(stmt, cannot_change_candidates, 2));
    record->attr_unbind_limit_time = get_int_column(stmt, find_column_index(stmt, unbind_limit_candidates, 4));
    record->attr_unbind_deduct_time = get_int_column(stmt, find_column_index(stmt, unbind_deduct_candidates, 2));
    record->attr_unbind_free_count = get_int_column(stmt, find_column_index(stmt, unbind_free_candidates, 2));
    record->attr_unbind_max_count = get_int_column(stmt, find_column_index(stmt, unbind_max_candidates, 2));
    record->bind_ip = get_int_column(stmt, find_column_index(stmt, bind_ip_candidates, 2));
    record->bind_machine_num = get_int_column(stmt, find_column_index(stmt, bind_machine_candidates, 2));
    record->lock_bind_pcsign = get_int_column(stmt, find_column_index(stmt, lock_bind_candidates, 3));
    record->activate_time = get_int64_column(stmt, find_column_index(stmt, activate_candidates, 2));
    record->expired_time = get_int64_column(stmt, find_column_index(stmt, expired_candidates, 2));
    record->last_login_time = get_int64_column(stmt, find_column_index(stmt, last_login_candidates, 2));
    record->delstate = get_int_column(stmt, find_column_index(stmt, delstate_candidates, 2));
    record->cty = get_int_column(stmt, find_column_index(stmt, city_candidates, 2));
    record->expired_time2 = get_int64_column(stmt, find_column_index(stmt, expired2_candidates, 3));

    return SQLITE_OK;
}

SP_EXPORT int sp_card_type_get_all(
    const char* db_path,
    sp_card_type_record** records,
    int* record_count,
    char** error_message)
{
    if (records)
    {
        *records = NULL;
    }
    if (record_count)
    {
        *record_count = 0;
    }

    if (!db_path)
    {
        set_error(error_message, "Invalid database path", NULL);
        return SQLITE_ERROR;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    int exists = 0;
    rc = table_exists(db, "CardType", &exists);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    if (!exists)
    {
        sqlite3_close(db);
        return SQLITE_OK;
    }

    const char* sql = "SELECT * FROM CardType ORDER BY Name ASC;";

    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    int capacity = 16;
    int count = 0;
    sp_card_type_record* buffer = NULL;
    if (records)
    {
        buffer = (sp_card_type_record*)malloc((size_t)capacity * sizeof(sp_card_type_record));
        if (!buffer)
        {
            finalize_stmt(&stmt);
            sqlite3_close(db);
            return SQLITE_NOMEM;
        }
    }

    while ((rc = sqlite3_step(stmt)) == SQLITE_ROW)
    {
        if (!buffer)
        {
            count++;
            continue;
        }

        if (count >= capacity)
        {
            int new_capacity = capacity * 2;
            sp_card_type_record* resized =
                (sp_card_type_record*)realloc(buffer, (size_t)new_capacity * sizeof(sp_card_type_record));
            if (!resized)
            {
                rc = SQLITE_NOMEM;
                break;
            }

            buffer = resized;
            capacity = new_capacity;
        }

        if (populate_card_type_record(stmt, &buffer[count]) != SQLITE_OK)
        {
            rc = SQLITE_NOMEM;
            break;
        }

        count++;
    }

    if (rc == SQLITE_DONE)
    {
        rc = SQLITE_OK;
    }

    finalize_stmt(&stmt);
    sqlite3_close(db);

    if (rc != SQLITE_OK)
    {
        if (buffer)
        {
            for (int i = 0; i < count; ++i)
            {
                free_card_type_record_internal(&buffer[i]);
            }
            free(buffer);
        }
        return rc;
    }

    if (records)
    {
        *records = buffer;
    }
    else if (buffer)
    {
        for (int i = 0; i < count; ++i)
        {
            free_card_type_record_internal(&buffer[i]);
        }
        free(buffer);
    }

    if (record_count)
    {
        *record_count = count;
    }

    return SQLITE_OK;
}

SP_EXPORT int sp_card_type_get_by_name(
    const char* db_path,
    const char* name,
    sp_card_type_record** record,
    int* has_value,
    char** error_message)
{
    if (record)
    {
        *record = NULL;
    }
    if (has_value)
    {
        *has_value = 0;
    }

    if (!db_path || !name)
    {
        set_error(error_message, "Invalid arguments", NULL);
        return SQLITE_ERROR;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    int exists = 0;
    rc = table_exists(db, "CardType", &exists);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    if (!exists)
    {
        sqlite3_close(db);
        return SQLITE_OK;
    }

    const char* sql = "SELECT * FROM CardType WHERE Name = ? LIMIT 1;";

    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    sqlite3_bind_text(stmt, 1, name, -1, SQLITE_TRANSIENT);

    rc = sqlite3_step(stmt);
    if (rc == SQLITE_ROW)
    {
        if (record)
        {
            sp_card_type_record* result = (sp_card_type_record*)malloc(sizeof(sp_card_type_record));
            if (!result)
            {
                rc = SQLITE_NOMEM;
            }
            else if (populate_card_type_record(stmt, result) != SQLITE_OK)
            {
                free(result);
                rc = SQLITE_NOMEM;
            }
            else
            {
                *record = result;
                if (has_value)
                {
                    *has_value = 1;
                }
                rc = SQLITE_OK;
            }
        }
        else
        {
            rc = SQLITE_OK;
            if (has_value)
            {
                *has_value = 1;
            }
        }
    }
    else if (rc == SQLITE_DONE)
    {
        rc = SQLITE_OK;
    }

    finalize_stmt(&stmt);
    sqlite3_close(db);
    return rc;
}

SP_EXPORT void sp_card_type_free_records(
    sp_card_type_record* records,
    int record_count)
{
    if (!records)
    {
        return;
    }

    for (int i = 0; i < record_count; ++i)
    {
        free_card_type_record_internal(&records[i]);
    }

    free(records);
}

SP_EXPORT void sp_card_type_free_record(sp_card_type_record* record)
{
    if (!record)
    {
        return;
    }

    free_card_type_record_internal(record);
    free(record);
}

static void free_card_record_internal(sp_card_record* record)
{
    if (!record)
    {
        return;
    }

    free(record->prefix_name);
    free(record->whom);
    free(record->card_type);
    free(record->state);
    free(record->ip);
    free(record->remarks);
    free(record->owner);
    free(record->pcsign2);
    if (record->user_extra_data)
    {
        free(record->user_extra_data);
    }

    record->prefix_name = NULL;
    record->whom = NULL;
    record->card_type = NULL;
    record->state = NULL;
    record->ip = NULL;
    record->remarks = NULL;
    record->owner = NULL;
    record->pcsign2 = NULL;
    record->user_extra_data = NULL;
    record->user_extra_data_length = 0;
}

static void free_card_binding_records(sp_card_binding_record* records, int record_count)
{
    if (!records)
    {
        return;
    }

    for (int i = 0; i < record_count; ++i)
    {
        free(records[i].card);
        free(records[i].pc_sign);
        records[i].card = NULL;
        records[i].pc_sign = NULL;
    }

    free(records);
}

static void free_activated_card_records(sp_activated_card_record* records, int record_count)
{
    if (!records)
    {
        return;
    }

    for (int i = 0; i < record_count; ++i)
    {
        free(records[i].card);
        records[i].card = NULL;
    }

    free(records);
}

static void free_card_trend_records(sp_card_trend_record* records, int record_count)
{
    if (!records)
    {
        return;
    }

    for (int i = 0; i < record_count; ++i)
    {
        free(records[i].whom);
        free(records[i].day);
        records[i].whom = NULL;
        records[i].day = NULL;
    }

    free(records);
}

static int populate_card_record(sqlite3_stmt* stmt, sp_card_record* record)
{
    if (!record)
    {
        return SQLITE_ERROR;
    }

    memset(record, 0, sizeof(*record));

    if (duplicate_text_column(stmt, 0, &record->prefix_name) != SQLITE_OK ||
        duplicate_text_column(stmt, 1, &record->whom) != SQLITE_OK ||
        duplicate_text_column(stmt, 2, &record->card_type) != SQLITE_OK ||
        duplicate_text_column(stmt, 4, &record->state) != SQLITE_OK ||
        duplicate_text_column(stmt, 8, &record->ip) != SQLITE_OK ||
        duplicate_text_column(stmt, 9, &record->remarks) != SQLITE_OK ||
        duplicate_text_column(stmt, 26, &record->owner) != SQLITE_OK ||
        duplicate_text_column(stmt, 30, &record->pcsign2) != SQLITE_OK)
    {
        free_card_record_internal(record);
        return SQLITE_NOMEM;
    }

    record->fyi = sqlite3_column_int(stmt, 3);
    record->bind = sqlite3_column_int(stmt, 5);
    record->open_num = sqlite3_column_int(stmt, 6);
    record->login_count = sqlite3_column_int(stmt, 7);
    record->create_data = sqlite3_column_int64(stmt, 10);
    record->activate_time = sqlite3_column_int64(stmt, 11);
    record->expired_time = sqlite3_column_int64(stmt, 12);
    record->last_login_time = sqlite3_column_int64(stmt, 13);
    record->delstate = sqlite3_column_int(stmt, 14);
    record->price = sqlite3_column_double(stmt, 15);
    record->cty = sqlite3_column_int(stmt, 16);
    record->expired_time2 = sqlite3_column_int64(stmt, 17);
    record->unbind_count = sqlite3_column_int(stmt, 18);
    record->unbind_deduct = sqlite3_column_int(stmt, 19);
    record->attr_unbind_limit_time = sqlite3_column_int(stmt, 20);
    record->attr_unbind_deduct_time = sqlite3_column_int(stmt, 21);
    record->attr_unbind_free_count = sqlite3_column_int(stmt, 22);
    record->attr_unbind_max_count = sqlite3_column_int(stmt, 23);
    record->bind_ip = sqlite3_column_int(stmt, 24);
    record->ban_time = sqlite3_column_int(stmt, 25);
    record->bind_user = sqlite3_column_int(stmt, 27);
    record->now_bind_machine_num = sqlite3_column_int(stmt, 28);
    record->bind_machine_num = sqlite3_column_int(stmt, 29);
    record->ban_duration_time = sqlite3_column_int(stmt, 31);
    record->give_back_ban_time = sqlite3_column_int(stmt, 32);
    record->picx_count = sqlite3_column_int(stmt, 33);
    record->lock_bind_pcsign = sqlite3_column_int(stmt, 34);
    record->last_recharge_time = sqlite3_column_int64(stmt, 35);

    const void* blob = sqlite3_column_blob(stmt, 36);
    int blob_size = sqlite3_column_bytes(stmt, 36);
    if (blob && blob_size > 0)
    {
        record->user_extra_data = (unsigned char*)malloc((size_t)blob_size);
        if (!record->user_extra_data)
        {
            free_card_record_internal(record);
            return SQLITE_NOMEM;
        }

        memcpy(record->user_extra_data, blob, (size_t)blob_size);
        record->user_extra_data_length = blob_size;
    }

    return SQLITE_OK;
}

SP_EXPORT int sp_card_get_by_key(
    const char* db_path,
    const char* card_key,
    sp_card_record** record,
    int* has_value,
    char** error_message)
{
    if (record)
    {
        *record = NULL;
    }
    if (has_value)
    {
        *has_value = 0;
    }

    if (!db_path || !card_key)
    {
        set_error(error_message, "Invalid arguments", NULL);
        return SQLITE_ERROR;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    int exists = 0;
    rc = table_exists(db, "CardInfo", &exists);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    if (!exists)
    {
        sqlite3_close(db);
        return SQLITE_OK;
    }

    const char* sql =
        "SELECT Prefix_Name, Whom, CardType, FYI, state, Bind, OpenNum, LoginCount, IP, Remarks, CreateData_, "
        "ActivateTime_, ExpiredTime_, LastLoginTime_, delstate, Price, cty, ExpiredTime__, UnBindCount, UnBindDeduct, "
        "Attr_UnBindLimitTime, Attr_UnBindDeductTime, Attr_UnBindFreeCount, Attr_UnBindMaxCount, BindIP, BanTime, Owner, "
        "BindUser, NowBindMachineNum, BindMachineNum, PCSign2, BanDurationTime, GiveBackBanTime, PICXCount, LockBindPcsign, "
        "LastRechargeTime, UserExtraData FROM CardInfo WHERE Prefix_Name = ? AND delstate = 0 LIMIT 1;";

    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    sqlite3_bind_text(stmt, 1, card_key, -1, SQLITE_TRANSIENT);

    rc = sqlite3_step(stmt);
    if (rc == SQLITE_ROW)
    {
        if (record)
        {
            sp_card_record* result = (sp_card_record*)malloc(sizeof(sp_card_record));
            if (!result)
            {
                rc = SQLITE_NOMEM;
            }
            else if (populate_card_record(stmt, result) != SQLITE_OK)
            {
                free(result);
                rc = SQLITE_NOMEM;
            }
            else
            {
                *record = result;
                if (has_value)
                {
                    *has_value = 1;
                }
                rc = SQLITE_OK;
            }
        }
        else
        {
            rc = SQLITE_OK;
            if (has_value)
            {
                *has_value = 1;
            }
        }
    }
    else if (rc == SQLITE_DONE)
    {
        rc = SQLITE_OK;
    }

    finalize_stmt(&stmt);
    sqlite3_close(db);
    return rc;
}

SP_EXPORT int sp_card_query_list(
    const char* db_path,
    int page,
    int page_size,
    const char* status,
    int search_type,
    const char** creators,
    int creator_count,
    const char** keywords,
    int keyword_count,
    sp_card_record** records,
    int* record_count,
    sp_card_binding_record** bindings,
    int* binding_count,
    long long* total,
    char** error_message)
{
    if (records)
    {
        *records = NULL;
    }
    if (record_count)
    {
        *record_count = 0;
    }
    if (bindings)
    {
        *bindings = NULL;
    }
    if (binding_count)
    {
        *binding_count = 0;
    }
    if (total)
    {
        *total = 0;
    }

    if (!db_path)
    {
        set_error(error_message, "Invalid database path", NULL);
        return SQLITE_ERROR;
    }

    int safe_page = page <= 0 ? 1 : page;
    int safe_page_size = page_size <= 0 ? 20 : page_size;
    if (safe_page_size <= 0)
    {
        safe_page_size = 20;
    }

    long long offset = (long long)(safe_page - 1);
    offset *= safe_page_size;

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    sqlite3_stmt* stmt = NULL;
    char* where_clause = NULL;
    char* count_sql = NULL;
    char* data_sql = NULL;
    const char** param_values = NULL;
    sp_card_record* result_records = NULL;
    sp_card_binding_record* binding_records = NULL;
    int result_count = 0;
    int binding_index = 0;

    int exists = 0;
    rc = table_exists(db, "CardInfo", &exists);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        goto cleanup;
    }

    if (!exists)
    {
        rc = SQLITE_OK;
        goto cleanup;
    }

    int has_status = status && status[0] != '\0' && std::strcmp(status, "0") != 0;
    int normalized_creator_count = creator_count > 0 ? creator_count : 0;
    int normalized_keyword_count = keyword_count > 0 ? keyword_count : 0;

    where_clause = sqlite3_mprintf("WHERE delstate = 0");
    if (!where_clause)
    {
        rc = SQLITE_NOMEM;
        goto cleanup;
    }

    int param_capacity = (has_status ? 1 : 0) + normalized_creator_count + normalized_keyword_count;
    if (param_capacity > 0)
    {
        param_values = (const char**)malloc((size_t)param_capacity * sizeof(const char*));
        if (!param_values)
        {
            rc = SQLITE_NOMEM;
            goto cleanup;
        }
    }

    int param_index = 0;

    if (has_status)
    {
        char* tmp = sqlite3_mprintf("%s AND state = ?", where_clause);
        sqlite3_free(where_clause);
        if (!tmp)
        {
            rc = SQLITE_NOMEM;
            goto cleanup;
        }
        where_clause = tmp;
        param_values[param_index++] = status;
    }

    if (normalized_creator_count > 0)
    {
        char* placeholders = NULL;
        rc = build_in_clause(normalized_creator_count, &placeholders);
        if (rc != SQLITE_OK || !placeholders)
        {
            rc = SQLITE_NOMEM;
            goto cleanup;
        }

        char* tmp = sqlite3_mprintf("%s AND Whom IN (%s)", where_clause, placeholders);
        sqlite3_free(placeholders);
        sqlite3_free(where_clause);
        if (!tmp)
        {
            rc = SQLITE_NOMEM;
            goto cleanup;
        }

        where_clause = tmp;

        for (int i = 0; i < normalized_creator_count; ++i)
        {
            param_values[param_index++] = creators ? creators[i] : NULL;
        }
    }

    if (normalized_keyword_count > 0)
    {
        if (search_type == 1)
        {
            char* placeholders = NULL;
            rc = build_in_clause(normalized_keyword_count, &placeholders);
            if (rc != SQLITE_OK || !placeholders)
            {
                rc = SQLITE_NOMEM;
                goto cleanup;
            }

            char* tmp = sqlite3_mprintf("%s AND Prefix_Name IN (%s)", where_clause, placeholders);
            sqlite3_free(placeholders);
            sqlite3_free(where_clause);
            if (!tmp)
            {
                rc = SQLITE_NOMEM;
                goto cleanup;
            }

            where_clause = tmp;

            for (int i = 0; i < normalized_keyword_count; ++i)
            {
                param_values[param_index++] = keywords ? keywords[i] : NULL;
            }
        }
        else if (search_type == 3)
        {
            char* placeholders = NULL;
            rc = build_in_clause(normalized_keyword_count, &placeholders);
            if (rc != SQLITE_OK || !placeholders)
            {
                rc = SQLITE_NOMEM;
                goto cleanup;
            }

            char* tmp = sqlite3_mprintf("%s AND CardType IN (%s)", where_clause, placeholders);
            sqlite3_free(placeholders);
            sqlite3_free(where_clause);
            if (!tmp)
            {
                rc = SQLITE_NOMEM;
                goto cleanup;
            }

            where_clause = tmp;

            for (int i = 0; i < normalized_keyword_count; ++i)
            {
                param_values[param_index++] = keywords ? keywords[i] : NULL;
            }
        }
        else
        {
            const char* column = (search_type == 2) ? "IP" : "Prefix_Name";
            char* conditions = build_or_like_clause(column, normalized_keyword_count);
            if (!conditions)
            {
                rc = SQLITE_NOMEM;
                goto cleanup;
            }

            char* tmp = sqlite3_mprintf("%s AND (%s)", where_clause, conditions);
            sqlite3_free(conditions);
            sqlite3_free(where_clause);
            if (!tmp)
            {
                rc = SQLITE_NOMEM;
                goto cleanup;
            }

            where_clause = tmp;

            for (int i = 0; i < normalized_keyword_count; ++i)
            {
                param_values[param_index++] = keywords ? keywords[i] : NULL;
            }
        }
    }

    count_sql = sqlite3_mprintf("SELECT COUNT(*) FROM CardInfo %s;", where_clause);
    const char* select_columns =
        "SELECT Prefix_Name, Whom, CardType, FYI, state, Bind, OpenNum, LoginCount, IP, Remarks, CreateData_, "
        "ActivateTime_, ExpiredTime_, LastLoginTime_, delstate, Price, cty, ExpiredTime__, UnBindCount, UnBindDeduct, "
        "Attr_UnBindLimitTime, Attr_UnBindDeductTime, Attr_UnBindFreeCount, Attr_UnBindMaxCount, BindIP, BanTime, Owner, "
        "BindUser, NowBindMachineNum, BindMachineNum, PCSign2, BanDurationTime, GiveBackBanTime, PICXCount, LockBindPcsign, "
        "LastRechargeTime, UserExtraData FROM CardInfo %s ORDER BY CreateData_ DESC LIMIT ? OFFSET ?;";
    data_sql = sqlite3_mprintf(select_columns, where_clause);

    if (!count_sql || !data_sql)
    {
        rc = SQLITE_NOMEM;
        goto cleanup;
    }

    rc = sqlite3_prepare_v2(db, count_sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        goto cleanup;
    }

    for (int i = 0; i < param_index; ++i)
    {
        const char* value = param_values ? param_values[i] : NULL;
        sqlite3_bind_text(stmt, i + 1, value ? value : "", -1, SQLITE_TRANSIENT);
    }

    rc = sqlite3_step(stmt);
    if (rc == SQLITE_ROW)
    {
        if (total)
        {
            *total = sqlite3_column_int64(stmt, 0);
        }
        rc = SQLITE_OK;
    }
    else if (rc == SQLITE_DONE)
    {
        rc = SQLITE_OK;
    }
    else
    {
        set_sqlite_error(error_message, db);
    }

    finalize_stmt(&stmt);
    if (rc != SQLITE_OK)
    {
        goto cleanup;
    }

    rc = sqlite3_prepare_v2(db, data_sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        goto cleanup;
    }

    for (int i = 0; i < param_index; ++i)
    {
        const char* value = param_values ? param_values[i] : NULL;
        sqlite3_bind_text(stmt, i + 1, value ? value : "", -1, SQLITE_TRANSIENT);
    }
    sqlite3_bind_int(stmt, param_index + 1, safe_page_size);
    sqlite3_bind_int64(stmt, param_index + 2, offset);

    int allocated = safe_page_size > 0 ? safe_page_size : 0;
    if (allocated > 0)
    {
        result_records = (sp_card_record*)calloc((size_t)allocated, sizeof(sp_card_record));
        if (!result_records)
        {
            rc = SQLITE_NOMEM;
            goto cleanup;
        }
    }

    while ((rc = sqlite3_step(stmt)) == SQLITE_ROW)
    {
        if (result_count >= allocated)
        {
            int new_capacity = allocated > 0 ? allocated * 2 : 16;
            sp_card_record* resized = (sp_card_record*)realloc(result_records, (size_t)new_capacity * sizeof(sp_card_record));
            if (!resized)
            {
                rc = SQLITE_NOMEM;
                break;
            }

            memset(resized + allocated, 0, (size_t)(new_capacity - allocated) * sizeof(sp_card_record));
            result_records = resized;
            allocated = new_capacity;
        }

        rc = populate_card_record(stmt, &result_records[result_count]);
        if (rc != SQLITE_OK)
        {
            break;
        }

        result_count++;
    }

    if (rc == SQLITE_DONE)
    {
        rc = SQLITE_OK;
    }
    else if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
    }

    finalize_stmt(&stmt);
    if (rc != SQLITE_OK)
    {
        goto cleanup;
    }

    sp_card_record* records_view = result_records;
    if (record_count)
    {
        *record_count = result_count;
    }
    if (records)
    {
        *records = result_records;
        records_view = *records;
        result_records = NULL;
    }
    else
    {
        records_view = result_records;
    }

    if (bindings && binding_count && result_count > 0 && records_view)
    {
        int bind_exists = 0;
        rc = table_exists(db, "BindInfo", &bind_exists);
        if (rc != SQLITE_OK)
        {
            set_sqlite_error(error_message, db);
            goto cleanup;
        }

        if (bind_exists)
        {
            char* placeholders = NULL;
            rc = build_in_clause(result_count, &placeholders);
            if (rc != SQLITE_OK || !placeholders)
            {
                rc = SQLITE_NOMEM;
                goto cleanup;
            }

            char* bind_sql = sqlite3_mprintf("SELECT Card, PCSign FROM BindInfo WHERE Card IN (%s);", placeholders);
            sqlite3_free(placeholders);
            if (!bind_sql)
            {
                rc = SQLITE_NOMEM;
                goto cleanup;
            }

            rc = sqlite3_prepare_v2(db, bind_sql, -1, &stmt, NULL);
            sqlite3_free(bind_sql);
            if (rc != SQLITE_OK)
            {
                set_sqlite_error(error_message, db);
                goto cleanup;
            }

            for (int i = 0; i < result_count; ++i)
            {
                const char* card_key = records_view[i].prefix_name ? records_view[i].prefix_name : "";
                sqlite3_bind_text(stmt, i + 1, card_key, -1, SQLITE_TRANSIENT);
            }

            int binding_capacity = result_count > 0 ? result_count : 16;
            if (binding_capacity < 16)
            {
                binding_capacity = 16;
            }
            binding_records = (sp_card_binding_record*)calloc((size_t)binding_capacity, sizeof(sp_card_binding_record));
            if (!binding_records)
            {
                rc = SQLITE_NOMEM;
                goto cleanup;
            }

            while ((rc = sqlite3_step(stmt)) == SQLITE_ROW)
            {
                if (binding_index >= binding_capacity)
                {
                    int new_capacity = binding_capacity * 2;
                    sp_card_binding_record* resized = (sp_card_binding_record*)realloc(
                        binding_records,
                        (size_t)new_capacity * sizeof(sp_card_binding_record));
                    if (!resized)
                    {
                        rc = SQLITE_NOMEM;
                        break;
                    }

                    memset(resized + binding_capacity, 0, (size_t)(new_capacity - binding_capacity) * sizeof(sp_card_binding_record));
                    binding_records = resized;
                    binding_capacity = new_capacity;
                }

                rc = duplicate_text_column(stmt, 0, &binding_records[binding_index].card);
                if (rc != SQLITE_OK)
                {
                    break;
                }

                rc = duplicate_text_column(stmt, 1, &binding_records[binding_index].pc_sign);
                if (rc != SQLITE_OK)
                {
                    break;
                }

                binding_index++;
            }

            if (rc == SQLITE_DONE)
            {
                rc = SQLITE_OK;
            }
            else if (rc != SQLITE_OK)
            {
                set_sqlite_error(error_message, db);
            }

            finalize_stmt(&stmt);
            if (rc != SQLITE_OK)
            {
                goto cleanup;
            }

            if (binding_count)
            {
                *binding_count = binding_index;
            }
            if (bindings)
            {
                *bindings = binding_records;
                binding_records = NULL;
            }
        }
    }

cleanup:
    if (rc != SQLITE_OK)
    {
        if (records)
        {
            *records = NULL;
        }
        if (record_count)
        {
            *record_count = 0;
        }
        if (bindings)
        {
            *bindings = NULL;
        }
        if (binding_count)
        {
            *binding_count = 0;
        }
        if (total)
        {
            *total = 0;
        }
    }

    finalize_stmt(&stmt);
    free(param_values);
    sqlite3_free(where_clause);
    sqlite3_free(count_sql);
    sqlite3_free(data_sql);

    if (binding_records)
    {
        free_card_binding_records(binding_records, binding_index);
    }

    if (result_records)
    {
        for (int i = 0; i < result_count; ++i)
        {
            free_card_record_internal(&result_records[i]);
        }
        free(result_records);
    }

    sqlite3_close(db);
    return rc;
}

SP_EXPORT int sp_card_insert_many(
    const char* db_path,
    const sp_card_insert_record* records,
    int record_count,
    char** error_message)
{
    if (!db_path)
    {
        set_error(error_message, "Invalid database path", NULL);
        return SQLITE_ERROR;
    }

    if (record_count <= 0)
    {
        return SQLITE_OK;
    }

    if (!records)
    {
        set_error(error_message, "Invalid card records", NULL);
        return SQLITE_ERROR;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    int exists = 0;
    rc = table_exists(db, "CardInfo", &exists);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    if (!exists)
    {
        sqlite3_close(db);
        return SQLITE_OK;
    }

    rc = begin_transaction(db, error_message);
    if (rc != SQLITE_OK)
    {
        sqlite3_close(db);
        return rc;
    }

    const char* sql =
        "INSERT INTO CardInfo "
        "(Prefix_Name, Whom, CardType, FYI, state, Bind, OpenNum, LoginCount, IP, Remarks, CreateData_, "
        " ActivateTime_, ExpiredTime_, LastLoginTime_, delstate, Price, cty, ExpiredTime__, "
        " Attr_UnBindLimitTime, Attr_UnBindDeductTime, Attr_UnBindFreeCount, Attr_UnBindMaxCount, "
        " BindIP, BindMachineNum, LockBindPcsign) "
        "VALUES (?, ?, ?, ?, ?, ?, ?, 0, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?);";

    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        rollback_transaction(db);
        sqlite3_close(db);
        return rc;
    }

    for (int i = 0; i < record_count; ++i)
    {
        const sp_card_insert_record* item = &records[i];
        sqlite3_clear_bindings(stmt);
        sqlite3_reset(stmt);

        sqlite3_bind_text(stmt, 1, item->prefix_name ? item->prefix_name : "", -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(stmt, 2, item->whom ? item->whom : "", -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(stmt, 3, item->card_type ? item->card_type : "", -1, SQLITE_TRANSIENT);
        sqlite3_bind_int(stmt, 4, item->fyi);
        sqlite3_bind_text(stmt, 5, item->state ? item->state : "", -1, SQLITE_TRANSIENT);
        sqlite3_bind_int(stmt, 6, item->bind);
        sqlite3_bind_int(stmt, 7, item->open_num);
        sqlite3_bind_text(stmt, 8, item->ip ? item->ip : "", -1, SQLITE_TRANSIENT);
        sqlite3_bind_text(stmt, 9, item->remarks ? item->remarks : "", -1, SQLITE_TRANSIENT);
        sqlite3_bind_int64(stmt, 10, item->create_data);
        sqlite3_bind_int64(stmt, 11, item->activate_time);
        sqlite3_bind_int64(stmt, 12, item->expired_time);
        sqlite3_bind_int64(stmt, 13, item->last_login_time);
        sqlite3_bind_int(stmt, 14, item->delstate);
        sqlite3_bind_double(stmt, 15, item->price);
        sqlite3_bind_int(stmt, 16, item->cty);
        sqlite3_bind_int64(stmt, 17, item->expired_time2);
        sqlite3_bind_int(stmt, 18, item->attr_unbind_limit_time);
        sqlite3_bind_int(stmt, 19, item->attr_unbind_deduct_time);
        sqlite3_bind_int(stmt, 20, item->attr_unbind_free_count);
        sqlite3_bind_int(stmt, 21, item->attr_unbind_max_count);
        sqlite3_bind_int(stmt, 22, item->bind_ip);
        sqlite3_bind_int(stmt, 23, item->bind_machine_num);
        sqlite3_bind_int(stmt, 24, item->lock_bind_pcsign);

        rc = sqlite3_step(stmt);
        if (rc != SQLITE_DONE)
        {
            set_sqlite_error(error_message, db);
            rc = SQLITE_ERROR;
            goto insert_cleanup;
        }
    }

    rc = commit_transaction(db, error_message);

insert_cleanup:
    if (rc != SQLITE_OK)
    {
        rollback_transaction(db);
    }

    finalize_stmt(&stmt);
    sqlite3_close(db);
    return rc;
}

SP_EXPORT int sp_card_query_activated(
    const char* db_path,
    const char* status,
    long long start_time,
    int has_start_time,
    long long end_time,
    int has_end_time,
    const char** creators,
    int creator_count,
    const char** card_types,
    int card_type_count,
    sp_activated_card_record** records,
    int* record_count,
    long long* total,
    char** error_message)
{
    if (records)
    {
        *records = NULL;
    }
    if (record_count)
    {
        *record_count = 0;
    }
    if (total)
    {
        *total = 0;
    }

    if (!db_path)
    {
        set_error(error_message, "Invalid database path", NULL);
        return SQLITE_ERROR;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    sqlite3_stmt* stmt = NULL;
    char* where_clause = NULL;
    char* count_sql = NULL;
    char* data_sql = NULL;
    sp_activated_card_record* result_records = NULL;
    int count = 0;

    sp_query_parameter_entry* parameters = NULL;

    int exists = 0;
    rc = table_exists(db, "CardInfo", &exists);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        goto activated_cleanup;
    }

    if (!exists)
    {
        rc = SQLITE_OK;
        goto activated_cleanup;
    }

    int has_status = status && status[0] != '\0' && std::strcmp(status, "0") != 0;
    int normalized_creator_count = creator_count > 0 ? creator_count : 0;
    int normalized_card_type_count = card_type_count > 0 ? card_type_count : 0;

    where_clause = sqlite3_mprintf("WHERE delstate = 0 AND ActivateTime_ > 0");
    if (!where_clause)
    {
        rc = SQLITE_NOMEM;
        goto activated_cleanup;
    }

    int parameter_capacity = (has_status ? 1 : 0) + normalized_creator_count + normalized_card_type_count +
        (has_start_time ? 1 : 0) + (has_end_time ? 1 : 0);
    if (parameter_capacity > 0)
    {
        parameters = (sp_query_parameter_entry*)malloc((size_t)parameter_capacity * sizeof(sp_query_parameter_entry));
        if (!parameters)
        {
            rc = SQLITE_NOMEM;
            goto activated_cleanup;
        }
    }

    int parameter_index = 0;

    if (has_status)
    {
        char* tmp = sqlite3_mprintf("%s AND state = ?", where_clause);
        sqlite3_free(where_clause);
        if (!tmp)
        {
            rc = SQLITE_NOMEM;
            goto activated_cleanup;
        }
        where_clause = tmp;

        parameters[parameter_index].is_text = 1;
        parameters[parameter_index].text = status;
        parameters[parameter_index].value = 0;
        parameter_index++;
    }

    if (normalized_creator_count > 0)
    {
        char* placeholders = NULL;
        rc = build_in_clause(normalized_creator_count, &placeholders);
        if (rc != SQLITE_OK || !placeholders)
        {
            rc = SQLITE_NOMEM;
            goto activated_cleanup;
        }

        char* tmp = sqlite3_mprintf("%s AND Whom IN (%s)", where_clause, placeholders);
        sqlite3_free(placeholders);
        sqlite3_free(where_clause);
        if (!tmp)
        {
            rc = SQLITE_NOMEM;
            goto activated_cleanup;
        }

        where_clause = tmp;

        for (int i = 0; i < normalized_creator_count; ++i)
        {
            parameters[parameter_index].is_text = 1;
            parameters[parameter_index].text = creators ? creators[i] : NULL;
            parameters[parameter_index].value = 0;
            parameter_index++;
        }
    }

    if (normalized_card_type_count > 0)
    {
        char* placeholders = NULL;
        rc = build_in_clause(normalized_card_type_count, &placeholders);
        if (rc != SQLITE_OK || !placeholders)
        {
            rc = SQLITE_NOMEM;
            goto activated_cleanup;
        }

        char* tmp = sqlite3_mprintf("%s AND CardType IN (%s)", where_clause, placeholders);
        sqlite3_free(placeholders);
        sqlite3_free(where_clause);
        if (!tmp)
        {
            rc = SQLITE_NOMEM;
            goto activated_cleanup;
        }

        where_clause = tmp;

        for (int i = 0; i < normalized_card_type_count; ++i)
        {
            parameters[parameter_index].is_text = 1;
            parameters[parameter_index].text = card_types ? card_types[i] : NULL;
            parameters[parameter_index].value = 0;
            parameter_index++;
        }
    }

    if (has_start_time)
    {
        char* tmp = sqlite3_mprintf("%s AND ActivateTime_ >= ?", where_clause);
        sqlite3_free(where_clause);
        if (!tmp)
        {
            rc = SQLITE_NOMEM;
            goto activated_cleanup;
        }

        where_clause = tmp;
        parameters[parameter_index].is_text = 0;
        parameters[parameter_index].text = NULL;
        parameters[parameter_index].value = start_time;
        parameter_index++;
    }

    if (has_end_time)
    {
        char* tmp = sqlite3_mprintf("%s AND ActivateTime_ <= ?", where_clause);
        sqlite3_free(where_clause);
        if (!tmp)
        {
            rc = SQLITE_NOMEM;
            goto activated_cleanup;
        }

        where_clause = tmp;
        parameters[parameter_index].is_text = 0;
        parameters[parameter_index].text = NULL;
        parameters[parameter_index].value = end_time;
        parameter_index++;
    }

    count_sql = sqlite3_mprintf("SELECT COUNT(*) FROM CardInfo %s;", where_clause);
    data_sql = sqlite3_mprintf("SELECT Prefix_Name, ActivateTime_ FROM CardInfo %s ORDER BY ActivateTime_ DESC;", where_clause);
    if (!count_sql || !data_sql)
    {
        rc = SQLITE_NOMEM;
        goto activated_cleanup;
    }

    rc = sqlite3_prepare_v2(db, count_sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        goto activated_cleanup;
    }

    for (int i = 0; i < parameter_index; ++i)
    {
        if (parameters[i].is_text)
        {
            sqlite3_bind_text(stmt, i + 1, parameters[i].text ? parameters[i].text : "", -1, SQLITE_TRANSIENT);
        }
        else
        {
            sqlite3_bind_int64(stmt, i + 1, parameters[i].value);
        }
    }

    rc = sqlite3_step(stmt);
    if (rc == SQLITE_ROW)
    {
        if (total)
        {
            *total = sqlite3_column_int64(stmt, 0);
        }
        rc = SQLITE_OK;
    }
    else if (rc == SQLITE_DONE)
    {
        rc = SQLITE_OK;
    }
    else
    {
        set_sqlite_error(error_message, db);
    }

    finalize_stmt(&stmt);
    if (rc != SQLITE_OK)
    {
        goto activated_cleanup;
    }

    rc = sqlite3_prepare_v2(db, data_sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        goto activated_cleanup;
    }

    for (int i = 0; i < parameter_index; ++i)
    {
        if (parameters[i].is_text)
        {
            sqlite3_bind_text(stmt, i + 1, parameters[i].text ? parameters[i].text : "", -1, SQLITE_TRANSIENT);
        }
        else
        {
            sqlite3_bind_int64(stmt, i + 1, parameters[i].value);
        }
    }

    int capacity = 64;
    result_records = (sp_activated_card_record*)calloc((size_t)capacity, sizeof(sp_activated_card_record));
    if (!result_records)
    {
        rc = SQLITE_NOMEM;
        goto activated_cleanup;
    }

    while ((rc = sqlite3_step(stmt)) == SQLITE_ROW)
    {
        if (count >= capacity)
        {
            int new_capacity = capacity * 2;
            sp_activated_card_record* resized = (sp_activated_card_record*)realloc(
                result_records,
                (size_t)new_capacity * sizeof(sp_activated_card_record));
            if (!resized)
            {
                rc = SQLITE_NOMEM;
                break;
            }

            memset(resized + capacity, 0, (size_t)(new_capacity - capacity) * sizeof(sp_activated_card_record));
            result_records = resized;
            capacity = new_capacity;
        }

        rc = duplicate_text_column(stmt, 0, &result_records[count].card);
        if (rc != SQLITE_OK)
        {
            break;
        }

        result_records[count].activate_time = sqlite3_column_int64(stmt, 1);
        count++;
    }

    if (rc == SQLITE_DONE)
    {
        rc = SQLITE_OK;
    }
    else if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
    }

    finalize_stmt(&stmt);
    if (rc != SQLITE_OK)
    {
        goto activated_cleanup;
    }

    if (record_count)
    {
        *record_count = count;
    }
    if (records)
    {
        *records = result_records;
        result_records = NULL;
    }

activated_cleanup:
    if (rc != SQLITE_OK)
    {
        if (records)
        {
            *records = NULL;
        }
        if (record_count)
        {
            *record_count = 0;
        }
        if (total)
        {
            *total = 0;
        }
    }

    finalize_stmt(&stmt);
    sqlite3_free(where_clause);
    sqlite3_free(count_sql);
    sqlite3_free(data_sql);

    if (result_records)
    {
        for (int i = 0; i < count; ++i)
        {
            free(result_records[i].card);
        }
        free(result_records);
    }

    free(parameters);
    sqlite3_close(db);
    return rc;
}

SP_EXPORT int sp_card_query_activation_trend(
    const char* db_path,
    long long start_time,
    long long end_time,
    const char** creators,
    int creator_count,
    int group_by_whom,
    sp_card_trend_record** records,
    int* record_count,
    char** error_message)
{
    if (records)
    {
        *records = NULL;
    }
    if (record_count)
    {
        *record_count = 0;
    }

    if (!db_path)
    {
        set_error(error_message, "Invalid database path", NULL);
        return SQLITE_ERROR;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    sqlite3_stmt* stmt = NULL;
    char* where_clause = NULL;
    char* sql = NULL;
    sp_card_trend_record* result_records = NULL;
    int count = 0;

    int exists = 0;
    rc = table_exists(db, "CardInfo", &exists);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        goto trend_cleanup;
    }

    if (!exists)
    {
        rc = SQLITE_OK;
        goto trend_cleanup;
    }

    int normalized_creator_count = creator_count > 0 ? creator_count : 0;

    where_clause = sqlite3_mprintf("WHERE delstate = 0 AND ActivateTime_ BETWEEN ? AND ?");
    if (!where_clause)
    {
        rc = SQLITE_NOMEM;
        goto trend_cleanup;
    }

    if (normalized_creator_count > 0)
    {
        char* placeholders = NULL;
        rc = build_in_clause(normalized_creator_count, &placeholders);
        if (rc != SQLITE_OK || !placeholders)
        {
            rc = SQLITE_NOMEM;
            goto trend_cleanup;
        }

        char* tmp = sqlite3_mprintf("%s AND Whom IN (%s)", where_clause, placeholders);
        sqlite3_free(placeholders);
        sqlite3_free(where_clause);
        if (!tmp)
        {
            rc = SQLITE_NOMEM;
            goto trend_cleanup;
        }
        where_clause = tmp;
    }

    const char* template_sql = group_by_whom
        ? "SELECT Whom, strftime('%%Y-%%m-%%d', datetime(ActivateTime_, 'unixepoch', 'localtime')) AS Day, COUNT(*) AS Count "
        "FROM CardInfo %s GROUP BY Whom, Day ORDER BY Day;"
        : "SELECT NULL AS Whom, strftime('%%Y-%%m-%%d', datetime(ActivateTime_, 'unixepoch', 'localtime')) AS Day, COUNT(*) AS Count "
        "FROM CardInfo %s GROUP BY Day ORDER BY Day;";

    sql = sqlite3_mprintf(template_sql, where_clause);
    if (!sql)
    {
        rc = SQLITE_NOMEM;
        goto trend_cleanup;
    }

    rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        goto trend_cleanup;
    }

    int bind_index = 1;
    sqlite3_bind_int64(stmt, bind_index++, start_time);
    sqlite3_bind_int64(stmt, bind_index++, end_time);

    for (int i = 0; i < normalized_creator_count; ++i)
    {
        sqlite3_bind_text(stmt, bind_index++, creators && creators[i] ? creators[i] : "", -1, SQLITE_TRANSIENT);
    }

    int capacity = 32;
    result_records = (sp_card_trend_record*)calloc((size_t)capacity, sizeof(sp_card_trend_record));
    if (!result_records)
    {
        rc = SQLITE_NOMEM;
        goto trend_cleanup;
    }

    while ((rc = sqlite3_step(stmt)) == SQLITE_ROW)
    {
        if (count >= capacity)
        {
            int new_capacity = capacity * 2;
            sp_card_trend_record* resized = (sp_card_trend_record*)realloc(
                result_records,
                (size_t)new_capacity * sizeof(sp_card_trend_record));
            if (!resized)
            {
                rc = SQLITE_NOMEM;
                break;
            }

            memset(resized + capacity, 0, (size_t)(new_capacity - capacity) * sizeof(sp_card_trend_record));
            result_records = resized;
            capacity = new_capacity;
        }

        rc = duplicate_text_column(stmt, 0, &result_records[count].whom);
        if (rc != SQLITE_OK)
        {
            break;
        }

        rc = duplicate_text_column(stmt, 1, &result_records[count].day);
        if (rc != SQLITE_OK)
        {
            break;
        }

        result_records[count].count = sqlite3_column_int64(stmt, 2);
        count++;
    }

    if (rc == SQLITE_DONE)
    {
        rc = SQLITE_OK;
    }
    else if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
    }

    finalize_stmt(&stmt);
    if (rc != SQLITE_OK)
    {
        goto trend_cleanup;
    }

    if (record_count)
    {
        *record_count = count;
    }
    if (records)
    {
        *records = result_records;
        result_records = NULL;
    }

trend_cleanup:
    if (rc != SQLITE_OK)
    {
        if (records)
        {
            *records = NULL;
        }
        if (record_count)
        {
            *record_count = 0;
        }
    }

    finalize_stmt(&stmt);
    sqlite3_free(where_clause);
    sqlite3_free(sql);

    if (result_records)
    {
        for (int i = 0; i < count; ++i)
        {
            free(result_records[i].whom);
            free(result_records[i].day);
        }
        free(result_records);
    }

    sqlite3_close(db);
    return rc;
}

SP_EXPORT void sp_card_free_records(sp_card_record* records, int record_count)
{
    if (!records)
    {
        return;
    }

    for (int i = 0; i < record_count; ++i)
    {
        free_card_record_internal(&records[i]);
    }

    free(records);
}

SP_EXPORT void sp_card_free_bindings(sp_card_binding_record* records, int record_count)
{
    free_card_binding_records(records, record_count);
}

SP_EXPORT void sp_card_free_activated(sp_activated_card_record* records, int record_count)
{
    free_activated_card_records(records, record_count);
}

SP_EXPORT void sp_card_free_trend(sp_card_trend_record* records, int record_count)
{
    free_card_trend_records(records, record_count);
}

SP_EXPORT void sp_card_free_record(sp_card_record* record)
{
    if (!record)
    {
        return;
    }

    free_card_record_internal(record);
    free(record);
}

SP_EXPORT int sp_card_delete_bindings(
    const char* db_path,
    const char* card_key,
    long long* affected_rows,
    char** error_message)
{
    if (affected_rows)
    {
        *affected_rows = 0;
    }

    if (!db_path || !card_key)
    {
        set_error(error_message, "Invalid arguments", NULL);
        return SQLITE_ERROR;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    int exists = 0;
    rc = table_exists(db, "BindInfo", &exists);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    if (!exists)
    {
        sqlite3_close(db);
        return SQLITE_OK;
    }

    const char* sql = "DELETE FROM BindInfo WHERE Card = ?;";
    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    sqlite3_bind_text(stmt, 1, card_key, -1, SQLITE_TRANSIENT);

    rc = sqlite3_step(stmt);
    if (rc == SQLITE_DONE)
    {
        rc = SQLITE_OK;
        if (affected_rows)
        {
            *affected_rows = sqlite3_changes(db);
        }
    }
    else
    {
        set_sqlite_error(error_message, db);
        rc = SQLITE_ERROR;
    }

    finalize_stmt(&stmt);
    sqlite3_close(db);
    return rc;
}

SP_EXPORT int sp_card_update_state(
    const char* db_path,
    const char* card_key,
    const char* state,
    int reset_ban_time,
    int reset_give_back_ban_time,
    char** error_message)
{
    if (!db_path || !card_key || !state)
    {
        set_error(error_message, "Invalid arguments", NULL);
        return SQLITE_ERROR;
    }

    sqlite3* db = NULL;
    int rc = open_database(db_path, &db, error_message);
    if (rc != SQLITE_OK)
    {
        return rc;
    }

    int exists = 0;
    rc = table_exists(db, "CardInfo", &exists);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    if (!exists)
    {
        sqlite3_close(db);
        return SQLITE_OK;
    }

    const char* sql =
        "UPDATE CardInfo SET state = ?, BanTime = CASE WHEN ? THEN 0 ELSE BanTime END, "
        "GiveBackBanTime = CASE WHEN ? THEN 0 ELSE GiveBackBanTime END WHERE Prefix_Name = ?;";

    sqlite3_stmt* stmt = NULL;
    rc = sqlite3_prepare_v2(db, sql, -1, &stmt, NULL);
    if (rc != SQLITE_OK)
    {
        set_sqlite_error(error_message, db);
        sqlite3_close(db);
        return rc;
    }

    sqlite3_bind_text(stmt, 1, state, -1, SQLITE_TRANSIENT);
    sqlite3_bind_int(stmt, 2, reset_ban_time ? 1 : 0);
    sqlite3_bind_int(stmt, 3, reset_give_back_ban_time ? 1 : 0);
    sqlite3_bind_text(stmt, 4, card_key, -1, SQLITE_TRANSIENT);

    rc = sqlite3_step(stmt);
    if (rc == SQLITE_DONE)
    {
        rc = SQLITE_OK;
    }
    else
    {
        set_sqlite_error(error_message, db);
        rc = SQLITE_ERROR;
    }

    finalize_stmt(&stmt);
    sqlite3_close(db);
    return rc;
}

SP_EXPORT void sp_free_error(char* message)
{
    if (message)
    {
        free(message);
    }
}
