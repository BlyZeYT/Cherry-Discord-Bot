namespace Cherry.Common;

using Discord.Addons.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using MySqlConnector;
using System.Data;
using System.Diagnostics;

public class Database : IDatabase
{
    private readonly ILogger<DiscordClientService> _logger;
    private readonly IConfiguration _config;
    private readonly MySqlConnection _connection;

    public Database(ILogger<DiscordClientService> logger, IConfiguration config)
    {
        _logger = logger;
        _config = config;
        _connection = new MySqlConnection(_config["database"]!);
    }

    public async Task<long> CheckConnectionAsync()
    {
        try
        {
            var sw = Stopwatch.StartNew();

            var con = _connection.OpenAsync();
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
        finally { await _connection.CloseAsync(); }
    }

    public async IAsyncEnumerable<ulong> GetAllGuildsAsync()
    {
        await ConnectAsync();

        using (MySqlDataReader reader = await new MySqlCommand($"SELECT * FROM guilds", _connection).ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                yield return ulong.Parse(reader.GetString("Guild_Id") ?? "0");
            }
        }
    }

    public async Task CreateGuildAsync(ulong guildId)
    {
        await ConnectAsync();

        try
        {
            await new MySqlCommand($"INSERT INTO guilds(Guild_Id, Guild_Prefix, Guild_Repeat) VALUES('{guildId}', '', '0')", _connection).ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDatabaseError(ex, nameof(CreateGuildAsync));
        }
        finally { await _connection.CloseAsync(); }
    }

    public async Task RemoveGuildAsync(ulong guildId)
    {
        await ConnectAsync();

        try
        {
            await new MySqlCommand($"DELETE FROM guilds WHERE Guild_Id = {guildId}", _connection).ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDatabaseError(ex, nameof(RemoveGuildAsync));
        }
        finally { await _connection.CloseAsync(); }
    }

    public async Task<string?> GetPrefixAsync(ulong guildId)
    {
        await ConnectAsync();

        object? prefix = null;

        try
        {
            prefix = await new MySqlCommand($"SELECT Guild_Prefix FROM guilds WHERE Guild_Id = {guildId}", _connection).ExecuteScalarAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDatabaseError(ex, nameof(GetPrefixAsync));
        }
        finally { await _connection.CloseAsync(); }

        return prefix is "" or null ? null : prefix.ToString();
    }

    public async Task<bool> SetPrefixAsync(ulong guildId, string prefix)
    {
        await ConnectAsync();

        var succeeded = 0;

        try
        {
            succeeded = await new MySqlCommand($"UPDATE guilds SET Guild_Prefix = '{prefix}' WHERE Guild_Id = {guildId}", _connection).ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDatabaseError(ex, nameof(SetPrefixAsync));
        }
        finally { await _connection.CloseAsync(); }

        return succeeded is not 0;
    }

    public async Task<bool> GetRepeatAsync(ulong guildId)
    {
        await ConnectAsync();

        var repeat = false;

        try
        {
            repeat = Convert.ToBoolean(await new MySqlCommand($"SELECT Guild_Repeat FROM guilds WHERE Guild_Id = {guildId}", _connection).ExecuteScalarAsync());
        }
        catch (Exception ex)
        {
            _logger.LogDatabaseError(ex, nameof(GetRepeatAsync));
        }
        finally { await _connection.CloseAsync(); }

        return repeat;
    }

    public async Task<bool> SetRepeatAsync(ulong guildId, bool repeat)
    {
        await ConnectAsync();

        var succeeded = 0;

        try
        {
            succeeded = await new MySqlCommand($"UPDATE guilds SET Guild_Repeat = {repeat} WHERE Guild_Id = {guildId}", _connection).ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            _logger.LogDatabaseError(ex, nameof(SetRepeatAsync));
        }
        finally { await _connection.CloseAsync(); }

        return succeeded is not 0;
    }

    private async Task ConnectAsync()
    {
        try
        {
            if (_connection.State is ConnectionState.Closed)
                await _connection.OpenAsync();
        }
        catch (Exception ex)
        {
            await _connection.CloseAsync();
            _logger.LogDatabaseError(ex, nameof(ConnectAsync));
        }
    }
}

public interface IDatabase
{
    public Task<long> CheckConnectionAsync();

    public IAsyncEnumerable<ulong> GetAllGuildsAsync();

    public Task CreateGuildAsync(ulong guildId);

    public Task RemoveGuildAsync(ulong guildId);

    public Task<string?> GetPrefixAsync(ulong guildId);

    public Task<bool> SetPrefixAsync(ulong guildId, string prefix);

    public Task<bool> GetRepeatAsync(ulong guildId);

    public Task<bool> SetRepeatAsync(ulong guildId, bool repeat);
}