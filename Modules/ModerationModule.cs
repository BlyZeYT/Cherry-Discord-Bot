namespace Cherry.Modules;

using Cherry.Common;
using Discord;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.Configuration;

[Name("Moderation")]
public class ModerationModule : CherryModuleBase
{
    private readonly IConfiguration _config;
    private readonly DiscordSocketClient _client;
    private readonly IDatabase _database;
    private readonly IEmbedSender _embed;

    public ModerationModule(IConfiguration config, DiscordSocketClient client, IDatabase database, IEmbedSender embed)
    {
        _config = config;
        _client = client;
        _database = database;
        _embed = embed;
    }

    [Command("prefix", RunMode = RunMode.Async)]
    [Summary("Changes the prefix for your server")]
    [Remarks("prefix <yourprefix>")]
    [RequireUserPermission(GuildPermission.Administrator, ErrorMessage = "You don't have the permission to change my prefix")]
    [RequireContext(ContextType.Guild)]
    public async Task Prefix([Remainder] string prefix = "")
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            await ReplyAsync($"My prefix is **{await _database.GetPrefixAsync(Context.Guild.Id) ?? _config["prefix"]}**");
            return;
        }

        if (prefix.Length > 10)
        {
            await ReplyAsync("You have to enter a prefix between 1 and 10 characters");
            return;
        }

        await ReplyAsync($"{(await _database.SetPrefixAsync(Context.Guild.Id, prefix.Trim()) ? $"My prefix is now **{prefix}**" : "Couldn't set new prefix!")}");
    }

    [Command("purge", RunMode = RunMode.Async)]
    [Alias("delete", "clear")]
    [Summary("Deletes X amount of messages up to 100")]
    [Remarks("purge <amount>")]
    [RequireUserPermission(GuildPermission.ManageMessages, ErrorMessage = "You don't have the permission to manage messages")]
    [RequireBotPermission(GuildPermission.ManageMessages, ErrorMessage = "I don't have the permission to manage messages")]
    [RequireContext(ContextType.Guild)]
    public async Task Purge([Remainder] string numberStr = "")
    {
        if (!int.TryParse(numberStr, out var amount))
        {
            await ReplyAsync("Please enter a number between 1 and 100");
            return;
        }

        if (amount is < 1 or > 100)
        {
            await ReplyAsync("Please enter a number between 1 and 100");
            return;
        }

        var messages = (await Context.Channel.GetMessagesAsync(amount + 1, CacheMode.AllowDownload).FlattenAsync())
            .Where(x => (DateTimeOffset.UtcNow - x.Timestamp).TotalDays <= 14);

        if (!messages.TryGetNonEnumeratedCount(out var count))
        {
            count = messages.Count();
        }

        IUserMessage message;

        if (count == 0)
        {
            message = await ReplyAsync("Nothing to delete");
        }
        else
        {
            await ((ITextChannel)Context.Channel).DeleteMessagesAsync(messages);
            message = await ReplyAsync($"Done! Removed {count - 1} {(count - 1 is not 1 ? "messages" : "message")}.");
        }

        await Task.Delay(2500);
        await message.DeleteAsync();
    }

    [Command("invite", RunMode = RunMode.Async)]
    [Summary("Invites a user by user id")]
    [Remarks("invite <userID> [expiration (hours)]")]
    [RequireUserPermission(GuildPermission.CreateInstantInvite, ErrorMessage = "You don't have the permission to invite anyone")]
    [RequireBotPermission(GuildPermission.CreateInstantInvite, ErrorMessage = "I don't have the permission to invite anyone")]
    [RequireContext(ContextType.Guild)]
    public async Task Invite(string user = "", [Remainder] string expirationStr = "")
    {
        if (!ulong.TryParse(user, out var userId))
        {
            await ReplyAsync("You have to enter an user id");
            return;
        }

        var inviteUser = await Context.Client.GetUserAsync(userId);

        if (inviteUser is null)
        {
            await ReplyAsync($"I can't invite an user that doesn't exist");
            return;
        }

        int? invitationHours = null;
        if (int.TryParse(expirationStr, out var expiration))
        {
            if (expiration is > 0 and < 25)
            {
                invitationHours = expiration;
            }
        }

        var invitation = await Context.Guild.DefaultChannel.CreateInviteAsync(invitationHours);

        var couldSend = await _embed.TrySendInvitationDMAsync(Context.User, inviteUser, Context.Guild, invitation);

        if (couldSend) await ReplyAsync($"Invited \\✉️: **{inviteUser.Username} #{inviteUser.Discriminator}**");
        else await ReplyAsync($"I can't invite {inviteUser.Username}");
    }

    [Command("role", RunMode = RunMode.Async)]
    [Summary("Gives or removes the mentioned user the mentioned role")]
    [Remarks("role <@user> <@role>")]
    [RequireUserPermission(GuildPermission.ManageRoles, ErrorMessage = "You don't have the permission to manage roles")]
    [RequireBotPermission(GuildPermission.ManageRoles, ErrorMessage = "I don't have the permission to manage roles")]
    [RequireContext(ContextType.Guild)]
    public async Task Role(SocketGuildUser? user = null, [Remainder] SocketRole? role = null)
    {
        if (user is null)
        {
            await ReplyAsync("You have to mention a user");
            return;
        }

        if (role is null)
        {
            await ReplyAsync("You have to mention a role");
            return;
        }

        if (role.IsManaged || role.IsEveryone)
        {
            await ReplyAsync("I can't work with this role");
            return;
        }

        if (Context.Guild.CurrentUser.Roles.Max()!.Position <= role.Position)
        {
            await ReplyAsync("I can't distribute a role that is higher or equal to mine");
            return;
        }

        if (user.Roles.Contains(role))
        {
            await user.RemoveRoleAsync(role);
            await ReplyAsync($"Removed {role.Mention} from **{user.Mention}**");
            return;
        }

        await user.AddRoleAsync(role);
        await ReplyAsync($"Added {role.Mention} to **{user.Mention}**");
    }

    [Command("kick", RunMode = RunMode.Async)]
    [Summary("Kicks the mentioned user from the server")]
    [Remarks("kick <@user> [reason]")]
    [RequireUserPermission(GuildPermission.KickMembers, ErrorMessage = "You don't have the permission to kick anyone")]
    [RequireBotPermission(GuildPermission.KickMembers, ErrorMessage = "I don't have the permission to kick anyone")]
    [RequireContext(ContextType.Guild)]
    public async Task Kick(SocketGuildUser? user = null, [Remainder] string? reason = null)
    {
        if (user is null)
        {
            await ReplyAsync("You have to mention a user");
            return;
        }

        if (((SocketGuildUser)Context.User).Hierarchy <= user.Hierarchy)
        {
            await ReplyAsync("You can't kick a user who is higher or equal to you in the hierarchy");
            return;
        }

        try
        {
            await user.KickAsync(reason);
        }
        catch (Exception)
        {
            await ReplyAsync("I can't kick this user");
            return;
        }

        await ReplyAsync($"Kicked \\⛔: **{user.Username} #{user.Discriminator}**{(reason is null ? "" : $"\nReason \\💬: {reason}")}");
    }

    [Command("ban", RunMode = RunMode.Async)]
    [Summary("Bans the mentioned user from the server")]
    [Remarks("ban <@user> [reason]")]
    [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "You don't have the permission to ban anyone")]
    [RequireBotPermission(GuildPermission.BanMembers, ErrorMessage = "I don't have the permission to ban anyone")]
    [RequireContext(ContextType.Guild)]
    public async Task Ban(SocketGuildUser? user = null, [Remainder] string? reason = null)
    {
        if (user is null)
        {
            await ReplyAsync("You have to mention a user");
            return;
        }

        if (((SocketGuildUser)Context.User).Hierarchy <= user.Hierarchy)
        {
            await ReplyAsync("You can't ban a user who is higher or equal to you in the hierarchy");
            return;
        }

        try
        {
            await user.BanAsync(0, reason);
        }
        catch (Exception)
        {
            await ReplyAsync("I can't ban this user");
            return;
        }

        await ReplyAsync($"Banned \\⛔: **{user.Username} #{user.Discriminator}**{(reason is null ? "" : $"\nReason \\💬: {reason}")}");
    }

    [Command("banlist", RunMode = RunMode.Async)]
    [Summary("Get a list of all banned users")]
    [Remarks("banlist")]
    [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "You don't have the permission to see the banlist")]
    [RequireBotPermission(GuildPermission.BanMembers, ErrorMessage = "I don't have the permission to see the banlist")]
    [RequireContext(ContextType.Guild)]
    public async Task Banlist()
        => await _embed.SendBanlistAsync((ITextChannel)Context.Channel, await Context.Guild.GetBansAsync().FlattenAsync());

    [Command("pardon", RunMode = RunMode.Async)]
    [Alias("unban")]
    [Summary("Pardons a user by user id")]
    [Remarks("pardon <userID>")]
    [RequireUserPermission(GuildPermission.BanMembers, ErrorMessage = "You don't have the permission to unban anyone")]
    [RequireBotPermission(GuildPermission.BanMembers, ErrorMessage = "I don't have the permission to unban anyone")]
    [RequireContext(ContextType.Guild)]
    public async Task Pardon([Remainder] string userStr = "")
    {
        if (!ulong.TryParse(userStr, out var userId))
        {
            await ReplyAsync("You have to enter an user id");
            return;
        }

        var user = await _client.GetUserAsync(userId);

        if (user is null)
        {
            await ReplyAsync($"I can't invite an user that doesn't exist");
            return;
        }

        try
        {
            await Context.Guild.RemoveBanAsync(user);
        }
        catch (Exception)
        {
            await ReplyAsync($"I can't unban this user");
            return;
        }

        await ReplyAsync($"Unbanned \\💚: **{user.Username} #{user.Discriminator}**");
    }
}
