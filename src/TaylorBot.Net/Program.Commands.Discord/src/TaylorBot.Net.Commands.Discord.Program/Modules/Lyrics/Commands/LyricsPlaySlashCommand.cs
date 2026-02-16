using Discord;
using Humanizer;
using TaylorBot.Net.Commands.Discord.Program.Modules.Taypoints.Domain;
using TaylorBot.Net.Commands.PostExecution;
using TaylorBot.Net.Core.Colors;
using TaylorBot.Net.Core.Embed;
using TaylorBot.Net.Core.Number;
using TaylorBot.Net.Core.Random;
using TaylorBot.Net.Core.User;

namespace TaylorBot.Net.Commands.Discord.Program.Modules.Lyrics.Commands;

// API: https://taylor.leox.dev/games/lyrics?length=<LENGTH>
// leave length blank for a random value
// length is capped and validated server-side, no need to validate it here, fallback to a random value if parsing fails
// calculated difficulty from server response is the reward granted in taypoints

public record LyricsResult(long invested_count, long final_count, long profit_count);

public record LyricsProfile(long lyrics_win_count, long lyrics_win_amount, long lyrics_lose_count, long lyrics_lose_amount);

public record LyricsLeaderboardEntry(string user_id, string username, long lyrics_win_count, long rank);

public interface ILyricsStatsRepository
{
    Task<LyricsProfile?> GetProfileAsync(DiscordUser user);
    Task<LyricsResult> WinAsync(DiscordUser user, ITaypointAmount amount, LyricsLevel level);
    Task<LyricsResult> LoseAsync(DiscordUser user, ITaypointAmount amount);
    Task<IList<LyricsLeaderboardEntry>> GetLeaderboardAsync(CommandGuild guild);
}

public class LyricsPlaySlashCommand(TaypointAmountParser amountParser, ILyricsStatsRepository lyricsStatsRepository, ICryptoSecureRandom cryptoSecureRandom, IPseudoRandom pseudoRandom) : ISlashCommand<LyricsPlaySlashCommand.Options>
{
    public const string PrefixCommandName = "lyrics";

    public static string CommandName => "lyrics play";

    public ISlashCommandInfo Info => new MessageCommandInfo(CommandName);

    public record Options(ITaypointAmount amount);

    public Command Play(RunContext context, DiscordUser author, ITaypointAmount? amount, string? amountString = null) => new(
        new(Info.Name, Aliases: [PrefixCommandName, PrefixSuperCommandName, PrefixSuperCommandAlias]),
        async () =>
        {
            if (amountString != null)
            {
                var parsed = await amountParser.ParseStringAsync(context, amountString);
                if (!parsed)
                {
                    return new EmbedResult(EmbedFactory.CreateError($"`amount`: {parsed.Error.Message}"));
                }
                amount = parsed.Value;
            }
            ArgumentNullException.ThrowIfNull(amount);

            level ??= LyricsLevel.Low;

            int winThreshold = level switch
            {
                LyricsLevel.Low => 51,
                LyricsLevel.Moderate => 76,
                LyricsLevel.High => 91,
                _ => throw new NotImplementedException(),
            };

            var randomNumber = cryptoSecureRandom.GetInt32(1, 100);

            var won = randomNumber >= winThreshold;

            var result = won
                ? await lyricsStatsRepository.WinAsync(author, amount, level.Value)
                : await lyricsStatsRepository.LoseAsync(author, amount);

            var originalCount = result.final_count - result.profit_count;

            var reason = pseudoRandom.GetRandomElement(won ? WinReasons : LoseReasons);

            var embed = new EmbedBuilder()
                .WithColor(won ? TaylorBotColors.SuccessColor : TaylorBotColors.ErrorColor)
                .WithDescription(
                    $"""
                    ### Opportunity ({level} Lyrics)
                    {reason.Opportunity}
                    You invest: **{"taypoint".ToQuantity(result.invested_count, TaylorBotFormats.Readable)} ({GetPercent(originalCount, result.invested_count):0%} of balance)** 💵
                    ### Outcome
                    **{(result.profit_count >= 0 ? "🟢 +" : "🔴 —")}{"taypoint".ToQuantity(Math.Abs(result.profit_count), TaylorBotFormats.Readable)}**
                    {reason.Outcome} {(won ? "💰" : "💸")}
                    Your balance: {originalCount.ToString(TaylorBotFormats.BoldReadable)} ➡️ {"taypoint".ToQuantity(result.final_count, TaylorBotFormats.BoldReadable)} {(won ? "📈" : "📉")}
                    """);

            return new EmbedResult(embed.Build());
        }
    );

    private static double GetPercent(long originalCount, long investedCount)
    {
        return originalCount != 0 ? (double)investedCount / originalCount : 0;
    }

    private sealed record Reason(string Opportunity, string Outcome);

    public ValueTask<Command> GetCommandAsync(RunContext context, Options options)
    {
        return new(Play(context, context.User, options.level, options.amount));
    }
}
