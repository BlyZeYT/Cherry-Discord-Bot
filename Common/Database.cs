namespace Cherry.Common;

using Discord.Addons.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System.Diagnostics;

public class Database : IDatabase
{
    private readonly ILogger<DiscordClientService> _logger;
    private readonly IConfiguration _config;

    public Database(ILogger<DiscordClientService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
        Connection = _config["database"]!;
    }

    private string Connection { get; init; }

    public async Task<long> CheckConnectionAsync()
    {
        using (var connection = new MySqlConnection(Connection))
        {
            try
            {
                var sw = Stopwatch.StartNew();

                var con = connection.OpenAsync();
                await con;

                while (!con.IsCompleted) { }

                sw.Stop();
                return sw.ElapsedMilliseconds;
            }
            catch (Exception ex)
            {
                _logger.LogDatabaseError(ex, nameof(CheckConnectionAsync));
                return -1;
            }
            finally { await connection.CloseAsync(); }
        }
    }

    public async Task<ulong[]> GetAllGuildsAsync()
    {
        using (var connection = new MySqlConnection(Connection))
        {
            var guilds = new List<ulong>();
            try
            {
                await connection.OpenAsync();
                var reader = await new MySqlCommand($"SELECT * FROM guilds", connection).ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    guilds.Add(ulong.Parse(reader["Guild_Id"].ToString() ?? "0"));
                }
            }
            catch (Exception ex)
            {
                _logger.LogDatabaseError(ex, nameof(GetAllGuildsAsync));
            }
            finally { await connection.CloseAsync(); }

            return guilds.ToArray();
        }
    }

    public async Task CreateGuildAsync(ulong guildId)
    {
        using (var connection = new MySqlConnection(Connection))
        {
            try
            {
                await connection.OpenAsync();
                await new MySqlCommand($"INSERT INTO guilds(Guild_Id, Guild_Prefix, Guild_Repeat) VALUES('{guildId}', '', '0')", connection).ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDatabaseError(ex, nameof(CreateGuildAsync));
            }
            finally { await connection.CloseAsync(); }
        }
    }

    public async Task RemoveGuildAsync(ulong guildId)
    {
        using (var connection = new MySqlConnection(Connection))
        {
            try
            {
                await connection.OpenAsync();
                await new MySqlCommand($"DELETE FROM guilds WHERE Guild_Id = {guildId}", connection).ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDatabaseError(ex, nameof(RemoveGuildAsync));
            }
            finally { await connection.CloseAsync(); }
        }
    }

    public async Task<string?> GetPrefixAsync(ulong guildId)
    {
        using (var connection = new MySqlConnection(Connection))
        {
            object? prefix = null;

            try
            {
                await connection.OpenAsync();
                prefix = await new MySqlCommand($"SELECT Guild_Prefix FROM guilds WHERE Guild_Id = {guildId}", connection).ExecuteScalarAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDatabaseError(ex, nameof(GetPrefixAsync));
            }
            finally { await connection.CloseAsync(); }

            return prefix is "" or null ? null : prefix.ToString();
        }
    }

    public async Task<bool> SetPrefixAsync(ulong guildId, string prefix)
    {
        using (var connection = new MySqlConnection(Connection))
        {
            var succeeded = 0;

            try
            {
                await connection.OpenAsync();
                succeeded = await new MySqlCommand($"UPDATE guilds SET Guild_Prefix = '{prefix}' WHERE Guild_Id = {guildId}", connection).ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDatabaseError(ex, nameof(SetPrefixAsync));
            }
            finally { await connection.CloseAsync(); }

            return succeeded is not 0;
        }
    }

    public async Task<bool> GetRepeatAsync(ulong guildId)
    {
        using (var connection = new MySqlConnection(Connection))
        {
            var repeat = false;

            try
            {
                await connection.OpenAsync();
                repeat = Convert.ToBoolean(await new MySqlCommand($"SELECT Guild_Repeat FROM guilds WHERE Guild_Id = {guildId}", connection).ExecuteScalarAsync());
            }
            catch (Exception ex)
            {
                _logger.LogDatabaseError(ex, nameof(GetRepeatAsync));
            }
            finally { await connection.CloseAsync(); }

            return repeat;
        }
    }

    public async Task<bool> SetRepeatAsync(ulong guildId, bool repeat)
    {
        using (var connection = new MySqlConnection(Connection))
        {
            var succeeded = 0;

            try
            {
                await connection.OpenAsync();
                succeeded = await new MySqlCommand($"UPDATE guilds SET Guild_Repeat = {repeat} WHERE Guild_Id = {guildId}", connection).ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                _logger.LogDatabaseError(ex, nameof(SetRepeatAsync));
            }
            finally { await connection.CloseAsync(); }

            return succeeded is not 0;
        }
    }
}

public interface IDatabase
{
    public Task<long> CheckConnectionAsync();

    public Task<ulong[]> GetAllGuildsAsync();

    public Task CreateGuildAsync(ulong guildId);

    public Task RemoveGuildAsync(ulong guildId);

    public Task<string?> GetPrefixAsync(ulong guildId);

    public Task<bool> SetPrefixAsync(ulong guildId, string prefix);

    public Task<bool> GetRepeatAsync(ulong guildId);

    public Task<bool> SetRepeatAsync(ulong guildId, bool repeat);
}