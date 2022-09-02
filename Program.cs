namespace Cherry;

using Cherry.Common;
using Discord;
using Discord.Addons.Hosting;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Victoria;

class Program
{
    static async Task Main()
    {
        var builder = new HostBuilder()
            .ConfigureAppConfiguration(x =>
            {
                var config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", false, true)
                .Build();

                x.AddConfiguration(config);
            })
            .ConfigureLogging(x =>
            {
                x.AddConsole();
                x.SetMinimumLevel(LogLevel.Error);
            })
            .ConfigureDiscordHost((context, config) =>
            {
                config.SocketConfig = new DiscordSocketConfig
                {
                    LogLevel = LogSeverity.Error,
                    AlwaysDownloadUsers = true,
                    MessageCacheSize = 1000,
                    GatewayIntents = GatewayIntents.All
                };

                config.Token = context.Configuration["token"]!;
            })
            .UseCommandService((context, config) => config = new CommandServiceConfig
            {
                CaseSensitiveCommands = false,
                LogLevel = LogSeverity.Error
            })
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<IDatabase, Database>();
                services.AddSingleton<IEmbedSender, EmbedSender>();
                services.AddSingleton<IMusicHelper, MusicHelper>();
                services.AddHostedService<CommandHandler>();
                services.AddLavaNode(x =>
                {
                    x.SelfDeaf = true;
                    x.EnableResume = true;
                    x.ReconnectDelay = TimeSpan.FromSeconds(1);
                    x.LogSeverity = LogSeverity.Error;
                    x.BufferSize = 2048;
                });
            })
            .UseConsoleLifetime();

        var host = builder.Build();
        using (host)
        {
            await host.RunAsync();
        }
    }
}
