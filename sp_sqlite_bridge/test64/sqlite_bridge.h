#ifndef SP_SQLITE_BRIDGE_H
#define SP_SQLITE_BRIDGE_H

#include <stddef.h>

#ifdef _WIN32
#define SP_EXPORT __declspec(dllexport)
#else
#define SP_EXPORT __attribute__((visibility("default")))
#endif

#ifdef __cplusplus
extern "C" {
#endif

    typedef struct sp_usage_distribution_entry {
        const char* whom;
        const char* payload;
        long long resolved_total;
        long long updated_at;
    } sp_usage_distribution_entry;

    typedef struct sp_usage_distribution_record {
        char* whom;
        char* payload;
        long long resolved_total;
        long long updated_at;
    } sp_usage_distribution_record;

    typedef struct sp_multi_software_record {
        char* software_name;
        int state;
        char* idc;
        int version;
    } sp_multi_software_record;

    SP_EXPORT int sp_multi_software_get_all(
        const char* db_path,
        sp_multi_software_record** records,
        int* record_count,
        char** error_message);

    SP_EXPORT void sp_multi_software_free_records(
        sp_multi_software_record* records,
        int record_count);

    SP_EXPORT int sp_usage_distribution_replace(
        const char* db_path,
        const char* software,
        const sp_usage_distribution_entry* entries,
        int entry_count,
        char** error_message);

    SP_EXPORT int sp_usage_distribution_get(
        const char* db_path,
        const char* software,
        const char** keys,
        int key_count,
        sp_usage_distribution_record** records,
        int* record_count,
        char** error_message);

    SP_EXPORT void sp_usage_distribution_free_records(
        sp_usage_distribution_record* records,
        int record_count);

    typedef struct sp_ip_location_record {
        char* ip;
        char* province;
        char* city;
        char* district;
        long long updated_at;
    } sp_ip_location_record;

    SP_EXPORT int sp_ip_location_get(
        const char* db_path,
        const char** ips,
        int ip_count,
        sp_ip_location_record** records,
        int* record_count,
        char** error_message);

    SP_EXPORT int sp_ip_location_upsert(
        const char* db_path,
        const sp_ip_location_record* records,
        int record_count,
        char** error_message);

    SP_EXPORT void sp_ip_location_free_records(
        sp_ip_location_record* records,
        int record_count);

    typedef struct sp_blacklist_machine_record {
        char* value;
        int type;
        char* remarks;
        long long row_id;
    } sp_blacklist_machine_record;

    typedef struct sp_blacklist_log_record {
        long long id;
        char* ip;
        char* card;
        char* pcsign;
        char* err_events;
        long long timestamp;
        long long row_id;
    } sp_blacklist_log_record;

    SP_EXPORT int sp_blacklist_get_machines(
        const char* db_path,
        int has_type_filter,
        int type_value,
        sp_blacklist_machine_record** records,
        int* record_count,
        char** error_message);

    SP_EXPORT int sp_blacklist_add_machine(
        const char* db_path,
        const char* value,
        int type,
        const char* remarks,
        char** error_message);

    SP_EXPORT int sp_blacklist_delete_machines(
        const char* db_path,
        const char** values,
        int value_count,
        char** error_message);

    SP_EXPORT void sp_blacklist_free_machines(
        sp_blacklist_machine_record* records,
        int record_count);

    SP_EXPORT int sp_blacklist_get_logs(
        const char* db_path,
        int limit,
        sp_blacklist_log_record** records,
        int* record_count,
        char** error_message);

    SP_EXPORT void sp_blacklist_free_logs(
        sp_blacklist_log_record* records,
        int record_count);

    typedef struct sp_agent_record {
        char* user;
        char* password;
        double account_balance;
        long long account_time;
        char* duration;
        char* authority;
        char* card_type_auth_name;
        int cards_enable;
        char* remarks;
        char* fnode;
        int stat;
        int deleted_at;
        long long duration_raw;
        double parities;
        double total_parities;
    } sp_agent_record;

    typedef struct sp_agent_statistics {
        long long total_cards;
        long long active_cards;
        long long used_cards;
        long long expired_cards;
        long long sub_agents;
    } sp_agent_statistics;

    SP_EXPORT int sp_agent_get_all(
        const char* db_path,
        sp_agent_record** records,
        int* record_count,
        char** error_message);

    SP_EXPORT int sp_agent_get_by_username(
        const char* db_path,
        const char* username,
        sp_agent_record** record,
        int* has_value,
        char** error_message);

    SP_EXPORT void sp_agent_free_records(
        sp_agent_record* records,
        int record_count);

    SP_EXPORT int sp_agent_set_status(
        const char* db_path,
        const char** usernames,
        int username_count,
        int enable,
        char** error_message);

    SP_EXPORT int sp_agent_update_remark(
        const char* db_path,
        const char* username,
        const char* remark,
        char** error_message);

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
        char** error_message);

    SP_EXPORT int sp_agent_soft_delete(
        const char* db_path,
        const char** usernames,
        int username_count,
        char** error_message);

    SP_EXPORT int sp_agent_update_password(
        const char* db_path,
        const char* username,
        const char* password,
        char** error_message);

    SP_EXPORT int sp_agent_add_balance(
        const char* db_path,
        const char* username,
        double balance,
        long long time_stock,
        char** error_message);

    SP_EXPORT int sp_agent_set_card_types(
        const char* db_path,
        const char* username,
        const char* card_types,
        char** error_message);

    SP_EXPORT int sp_agent_get_statistics(
        const char* db_path,
        const char* username,
        sp_agent_statistics* statistics,
        char** error_message);

    typedef struct sp_card_creator_record {
        char* whom;
    } sp_card_creator_record;

    typedef struct sp_card_ip_record {
        char* value;
    } sp_card_ip_record;

    SP_EXPORT int sp_card_get_creators(
        const char* db_path,
        sp_card_creator_record** records,
        int* record_count,
        char** error_message);

    SP_EXPORT void sp_card_free_creators(
        sp_card_creator_record* records,
        int record_count);

    SP_EXPORT int sp_card_get_ips(
        const char* db_path,
        const char** creators,
        int creator_count,
        sp_card_ip_record** records,
        int* record_count,
        char** error_message);

    SP_EXPORT void sp_card_free_ips(
        sp_card_ip_record* records,
        int record_count);

    typedef struct sp_card_type_record {
        char* name;
        char* prefix;
        int duration;
        int fyi;
        double price;
        char* param;
        int bind;
        int open_num;
        char* remarks;
        int cannot_be_changed;
        int attr_unbind_limit_time;
        int attr_unbind_deduct_time;
        int attr_unbind_free_count;
        int attr_unbind_max_count;
        int bind_ip;
        int bind_machine_num;
        int lock_bind_pcsign;
        long long activate_time;
        long long expired_time;
        long long last_login_time;
        int delstate;
        int cty;
        long long expired_time2;
    } sp_card_type_record;

    SP_EXPORT int sp_card_type_get_all(
        const char* db_path,
        sp_card_type_record** records,
        int* record_count,
        char** error_message);

    SP_EXPORT int sp_card_type_get_by_name(
        const char* db_path,
        const char* name,
        sp_card_type_record** record,
        int* has_value,
        char** error_message);

    SP_EXPORT void sp_card_type_free_records(
        sp_card_type_record* records,
        int record_count);

    SP_EXPORT void sp_card_type_free_record(
        sp_card_type_record* record);

    typedef struct sp_card_record {
        char* prefix_name;
        char* whom;
        char* card_type;
        int fyi;
        char* state;
        int bind;
        int open_num;
        int login_count;
        char* ip;
        char* remarks;
        long long create_data;
        long long activate_time;
        long long expired_time;
        long long last_login_time;
        int delstate;
        double price;
        int cty;
        long long expired_time2;
        int unbind_count;
        int unbind_deduct;
        int attr_unbind_limit_time;
        int attr_unbind_deduct_time;
        int attr_unbind_free_count;
        int attr_unbind_max_count;
        int bind_ip;
        int ban_time;
        char* owner;
        int bind_user;
        int now_bind_machine_num;
        int bind_machine_num;
        char* pcsign2;
        int ban_duration_time;
        int give_back_ban_time;
        int picx_count;
        int lock_bind_pcsign;
        long long last_recharge_time;
        unsigned char* user_extra_data;
        int user_extra_data_length;
    } sp_card_record;

    typedef struct sp_card_binding_record {
        char* card;
        char* pc_sign;
    } sp_card_binding_record;

    typedef struct sp_card_insert_record {
        const char* prefix_name;
        const char* whom;
        const char* card_type;
        int fyi;
        const char* state;
        int bind;
        int open_num;
        const char* ip;
        const char* remarks;
        long long create_data;
        long long activate_time;
        long long expired_time;
        long long last_login_time;
        int delstate;
        double price;
        int cty;
        long long expired_time2;
        int attr_unbind_limit_time;
        int attr_unbind_deduct_time;
        int attr_unbind_free_count;
        int attr_unbind_max_count;
        int bind_ip;
        int bind_machine_num;
        int lock_bind_pcsign;
    } sp_card_insert_record;

    typedef struct sp_activated_card_record {
        char* card;
        long long activate_time;
    } sp_activated_card_record;

    typedef struct sp_card_trend_record {
        char* whom;
        char* day;
        long long count;
    } sp_card_trend_record;

    SP_EXPORT int sp_card_get_by_key(
        const char* db_path,
        const char* card_key,
        sp_card_record** record,
        int* has_value,
        char** error_message);

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
        char** error_message);

    SP_EXPORT void sp_card_free_records(
        sp_card_record* records,
        int record_count);

    SP_EXPORT void sp_card_free_bindings(
        sp_card_binding_record* records,
        int record_count);

    SP_EXPORT int sp_card_insert_many(
        const char* db_path,
        const sp_card_insert_record* records,
        int record_count,
        char** error_message);

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
        char** error_message);

    SP_EXPORT void sp_card_free_activated(
        sp_activated_card_record* records,
        int record_count);

    SP_EXPORT int sp_card_query_activation_trend(
        const char* db_path,
        long long start_time,
        long long end_time,
        const char** creators,
        int creator_count,
        int group_by_whom,
        sp_card_trend_record** records,
        int* record_count,
        char** error_message);

    SP_EXPORT void sp_card_free_trend(
        sp_card_trend_record* records,
        int record_count);

    SP_EXPORT void sp_card_free_record(
        sp_card_record* record);

    SP_EXPORT int sp_card_delete_bindings(
        const char* db_path,
        const char* card_key,
        long long* affected_rows,
        char** error_message);

    SP_EXPORT int sp_card_update_state(
        const char* db_path,
        const char* card_key,
        const char* state,
        int reset_ban_time,
        int reset_give_back_ban_time,
        char** error_message);

    SP_EXPORT void sp_free_error(char* message);

#ifdef __cplusplus
}
#endif

#endif // SP_SQLITE_BRIDGE_H
