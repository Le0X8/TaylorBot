using Discord.Commands;
using TaylorBot.Net.Commands.DiscordNet;

namespace TaylorBot.Net.Commands.Discord.Program.Modules.Lyrics.Commands;

[Name("Lyrics Quiz 🎶")]
public class LyricsModule(ICommandRunner commandRunner, LyricsPlaySlashCommand lyricsCommand, PrefixedCommandRunner prefixedCommandRunner) : TaylorBotModule
{
  [Command(LyricsPlaySlashCommand.PrefixCommandName)]
  public async Task<RuntimeResult> LyricsAsync(
        [Remainder]
        string length
    )
  {
    var context = DiscordNetContextMapper.MapToRunContext(Context, new(ReplacementSlashCommand: LyricsPlaySlashCommand.CommandName));
    var result = await commandRunner.RunSlashCommandAsync(
        lyricsCommand.Play(context, context.User, amount: null, amountString: length),
        context
    );

    return new TaylorBotResult(result, context);
  }
}