using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SProtectAgentWeb.Api.Database;
using SProtectAgentWeb.Api.Models;
using SProtectAgentWeb.Api.Native;
using SProtectAgentWeb.Api.Utilities;

namespace SProtectAgentWeb.Api.Services
{
    public class AuthService
    {
        private readonly DatabaseManager _databaseManager;
        private readonly PermissionHelper _permissionHelper;
        private readonly ILogger<AuthService> _logger;

        public AuthService(DatabaseManager databaseManager, PermissionHelper permissionHelper, ILogger<AuthService> logger)
        {
            _databaseManager = databaseManager;
            _permissionHelper = permissionHelper;
            _logger = logger;
        }

        public async Task<UserSession> CreateUserSessionAsync(string username, string password, string? ipAddress, string? userAgent)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException("用户名或密码不能为空");

            var session = new UserSession
            {
                Username = username,
                Password = password,
                IpAddress = ipAddress,
            };

            var accessibleSoftwares = new List<string>();
            var softwareAgentInfo = new Dictionary<string, Agent>(StringComparer.OrdinalIgnoreCase);
            var hasDisabledAgent = false;

            // 默认数据库
            var defaultAgent = await TryLoadAgentAsync("默认软件", username, password);
            if (defaultAgent is not null)
            {
                if (defaultAgent.Stat == 1)
                {
                    hasDisabledAgent = true;
                }
                else
                {
                    accessibleSoftwares.Add("默认软件");
                    softwareAgentInfo["默认软件"] = defaultAgent;
                }
            }

            // 其它软件位
            var softwares = await _databaseManager.GetEnabledSoftwaresAsync();
            foreach (var software in softwares)
            {
                var agent = await TryLoadAgentAsync(software, username, password);
                if (agent != null)
                {
                    if (agent.Stat == 1)
                    {
                        hasDisabledAgent = true;
                        continue;
                    }

                    accessibleSoftwares.Add(software);
                    softwareAgentInfo[software] = agent;
                }
            }

            if (softwareAgentInfo.Count == 0)
            {
                if (hasDisabledAgent)
                    throw new InvalidOperationException("代理已禁用");

                throw new InvalidOperationException("用户名或密码错误");
            }

            session.SoftwareList = accessibleSoftwares.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            session.SoftwareAgentInfo = softwareAgentInfo;
            return session;
        }

        public async Task<UserSession> RefreshUserInfoAsync(UserSession existingSession)
        {
            return await CreateUserSessionAsync(existingSession.Username, existingSession.Password, existingSession.IpAddress, null);
        }

        public async Task ChangePasswordAsync(string username, string software, string oldPassword, string newPassword)
        {
            if (!SqliteBridge.IsNativeAvailable)
            {
                throw new DllNotFoundException("未找到 sp_sqlite_bridge 原生库，无法修改密码。请确保该库位于应用程序目录。");
            }

            var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
            var record = await Task.Run(() => SqliteBridge.GetAgent(dbPath, username)).ConfigureAwait(false);

            if (record is null || !string.Equals(record.Value.Password, oldPassword, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("原密码不正确或代理不存在");
            }

            await Task.Run(() => SqliteBridge.UpdateAgentPassword(dbPath, username, newPassword)).ConfigureAwait(false);
        }

        public async Task<UserSession?> ValidateSessionAsync(string username, string password)
        {
            try
            {
                return await CreateUserSessionAsync(username, password, null, null);
            }
            catch (DllNotFoundException)
            {
                throw;
            }
            catch (EntryPointNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to validate session for {Username}", username);
                return null;
            }
        }

        private static Agent MapAgent(SqliteBridge.AgentRecord record)
        {
            return new Agent
            {
                User = record.User,
                Password = record.Password,
                AccountBalance = record.AccountBalance,
                AccountTime = record.AccountTime,
                Duration = record.Duration,
                Authority = record.Authority,
                CardTypeAuthName = record.CardTypeAuthName,
                CardsEnable = record.CardsEnable != 0,
                Remarks = record.Remarks,
                FNode = record.FNode,
                Stat = record.Stat,
                Deltm = record.DeletedAt,
                Duration_ = record.DurationRaw,
                Parities = record.Parities,
                TatalParities = record.TotalParities,
            };
        }

        private async Task<Agent?> TryLoadAgentAsync(string software, string username, string password)
        {
            if (!SqliteBridge.IsNativeAvailable)
            {
                throw new DllNotFoundException("未找到 sp_sqlite_bridge 原生库，无法加载代理数据。请确保该库位于应用程序目录。");
            }

            try
            {
                var dbPath = await _databaseManager.PrepareDatabasePathAsync(software).ConfigureAwait(false);
                var record = await Task.Run(() => SqliteBridge.GetAgent(dbPath, username)).ConfigureAwait(false);

                if (record is null)
                {
                    return null;
                }

                if (!string.Equals(record.Value.Password, password, StringComparison.Ordinal))
                {
                    return null;
                }

                var agent = MapAgent(record.Value);
                agent.CardTypeAuthNameArray = _permissionHelper.ParseBracketList(agent.CardTypeAuthName).ToList();
                return agent;
            }
            catch (DllNotFoundException)
            {
                throw;
            }
            catch (EntryPointNotFoundException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load agent {Username} for software {Software}", username, software);
                return null;
            }
        }
    }
}
