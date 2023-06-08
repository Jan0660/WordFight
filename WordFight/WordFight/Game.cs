using System.Diagnostics;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Console = Log73.Console;

namespace WordFight;

public static class Game
{
    private static readonly List<Player> Players = new();
    public static readonly Player SoloPlayer = new()
    {
        Name = "-",
    };

    public const int MatchmakingTimeout = 12000;

    public static void NewPlayer(Player player)
    {
        // make sure we don't have a player with the same name
        if (Players.FirstOrDefault(a => a.Name == player.Name) is { } existingPlayer)
        {
            if (!existingPlayer.SocketTaskCompletionSource?.Task.IsCompleted ?? false)
                existingPlayer.SocketTaskCompletionSource.SetResult();
            player.IncorrectAnswers = existingPlayer.IncorrectAnswers;
            player.CorrectAnswers = existingPlayer.CorrectAnswers;
            Players.Remove(existingPlayer);
        }

        Players.Add(player);
#pragma warning disable CS4014
        Run(player);
#pragma warning restore CS4014
    }

    public static async Task Run(Player player)
    {
        var webSocket = player.Socket;
        Debug.Assert(webSocket != null);
        var buffer = new byte[1024 * 32];
        var receiveResult = await webSocket.ReceiveAsync(
            new ArraySegment<byte>(buffer), CancellationToken.None);
        var timer = new System.Timers.Timer(TimeSpan.FromMilliseconds(3200));
        timer.Elapsed += async (sender, args) =>
        {
            if (player.Socket.CloseStatus != null)
            {
                timer.Stop();
                return;
            }

            try
            {
                await player.SendAsync(new LeaderboardPacket()
                {
                    Players = Players.ToArray(),
                });
            }
            catch
            {
                // nothing :)
            }
        };
        timer.Start();

        while (!receiveResult.CloseStatus.HasValue)
        {
            var packet = JsonSerializer.Deserialize<ServerboundPacket>(
                buffer.AsSpan(0, receiveResult.Count),
                Globals.JsonOptions)!;
            Console.Debug($"Received packet: {packet}");

            switch (packet)
            {
                case StartPacket startPacket:
                    player.Status = PlayerStatus.Waiting;
                    // try to matchmake
                    // if there are only players the player has already played with, just wait
                    // if over MatchmakingTimeout the player will play with the first player in the list
                    var player2 = Players.FirstOrDefault(a =>
                        a.Status == PlayerStatus.Waiting && a != player &&
                        !player.PlayedWith.Contains(a.Name));

                    if (startPacket.PlaySolo)
                        player2 = SoloPlayer;
                    if (player2 == null)
                    {
                        var anyPlayer = () =>
                            Players.FirstOrDefault(a => a.Status == PlayerStatus.Waiting && a != player);
                        if (anyPlayer() != null)
                        {
                            await Task.Delay(MatchmakingTimeout);
                            player2 = anyPlayer();
                            // var stopwatch = Stopwatch.StartNew();
                            // while (anyPlayer() == null && stopwatch.ElapsedMilliseconds < MatchmakingTimeout)
                            //     await Task.Delay(100);
                            // player2 = anyPlayer();
                        }
                    }

                    if (player2 != null)
                    {
                        player.Status = PlayerStatus.Playing;
                        player2.Status = PlayerStatus.Playing;
                        await player.SendAsync(new MatchPacket()
                        {
                            OtherPlayerName = player2.Name,
                        });
                        await player2.SendAsync(new MatchPacket()
                        {
                            OtherPlayerName = player.Name,
                        });
                        var match = new Match()
                        {
                            Player1 = player,
                            Player2 = player2,
                        };
                        player.Match = match;
                        player2.Match = match;
                        player.PlayedWith.Add(player2.Name);
                        player2.PlayedWith.Add(player.Name);
                        await StartRound(match);
                    }

                    break;
                case AnswerPacket answerPacket:
                {
                    var match = player.Match;
                    if (match == null)
                    {
                        await webSocket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Invalid packet",
                            CancellationToken.None);
                        return;
                    }

                    var otherPlayer = match.Player1 == player ? match.Player2 : match.Player1;
                    var answerStatus = answerPacket.Answer == match.Prompt.Answer
                        ? AnswerStatus.Correct
                        : AnswerStatus.Incorrect;
                    await player.SendAsync(new AnswerStatusPacket()
                    {
                        AnswerStatus = answerStatus,
                    });
                    if (match.Player2 == SoloPlayer)
                        match.Player2Answer = AnswerStatus.Correct;
                    if (match.Player1 == player)
                        match.Player1Answer = answerStatus;
                    else
                        match.Player2Answer = answerStatus;
                    if (answerStatus == AnswerStatus.Correct)
                        player.CorrectAnswers++;
                    else
                        player.IncorrectAnswers++;
                    var bothAnswered = match.Player1Answer != AnswerStatus.Unanswered &&
                                       match.Player2Answer != AnswerStatus.Unanswered;
                    if (bothAnswered)
                    {
                        var player1Answer = match.Player1Answer;
                        var player2Answer = match.Player2Answer;
                        match.ResetAnswers();
                        Console.Debug("Both answered");
                        await Task.Delay(1800);
                        // if one is incorrect
                        if ((player1Answer == AnswerStatus.Incorrect ||
                             player2Answer == AnswerStatus.Incorrect) && match.Player2 != SoloPlayer)
                        {
                            Console.Debug("Match end");
                            await match.SendAsync(new MatchEndPacket()
                            {
                                WinnerName = player1Answer == AnswerStatus.Correct
                                    ? match.Player1.Name
                                    : match.Player2.Name,
                            });
                            player.Status = PlayerStatus.Neither;
                            otherPlayer.Status = PlayerStatus.Neither;
                            player.Match = null;
                            otherPlayer.Match = null;
                        }
                        else
                        {
                            // start new round
                            await StartRound(match);
                        }
                    }

                    break;
                }
            }

            receiveResult = await webSocket.ReceiveAsync(
                new ArraySegment<byte>(buffer), CancellationToken.None);
        }

        Console.Debug($"{player.Name} disconnected");
        timer.Stop();
        await webSocket.CloseAsync(
            receiveResult.CloseStatus.Value,
            receiveResult.CloseStatusDescription,
            CancellationToken.None);
        player.Status = PlayerStatus.Dead;
        player.SocketTaskCompletionSource?.SetResult();
    }

    private static async Task StartRound(Match match)
    {
        Console.Debug("Starting round");
        match.ResetAnswers();
        var word = Globals.Data.Words[Random.Shared.Next(Globals.Data.Words.Length)];
        var prompt = new Prompt()
        {
            Text = word.Text,
            Answer = word.Irregular ?? word.Class.ToString().ToLowerInvariant(),
            Options = word.GetOptions(),
        };
        match.Prompt = prompt;
        await match.SendAsync(new PromptPacket
        {
            Prompt = prompt,
        });
    }
}

public class Player
{
    public required string Name { get; init; }
    public PlayerStatus Status { get; set; } = PlayerStatus.Neither;
    [JsonIgnore] public WebSocket? Socket { get; init; }
    [JsonIgnore] public TaskCompletionSource? SocketTaskCompletionSource { get; init; }
    [JsonIgnore] public Match? Match { get; set; }
    public int CorrectAnswers { get; set; } = 0;
    public int IncorrectAnswers { get; set; } = 0;
    [JsonInclude] public int TotalAnswers => CorrectAnswers + IncorrectAnswers;
    public List<string> PlayedWith { get; set; } = new();

    public async Task SendAsync(ClientboundPacket packet)
    {
        if (Socket != null)
            await Socket.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(packet, Globals.JsonOptions)),
                WebSocketMessageType.Text, true, CancellationToken.None);
    }
}

public enum PlayerStatus
{
    Waiting,
    Playing,
    Neither,
    Dead,
}

public class Match
{
    public required Player Player1 { get; init; }
    public required Player Player2 { get; init; }
    public AnswerStatus Player1Answer { get; set; } = AnswerStatus.Unanswered;
    public AnswerStatus Player2Answer { get; set; } = AnswerStatus.Unanswered;
    public Prompt Prompt { get; set; } = null!;

    public void ResetAnswers()
    {
        Player1Answer = AnswerStatus.Unanswered;
        if (Player2 != Game.SoloPlayer)
            Player2Answer = AnswerStatus.Unanswered;
    }

    public async Task SendAsync(ClientboundPacket packet)
    {
        // await Player1.SendAsync(packet);
        // await Player2.SendAsync(packet);
        await Task.WhenAll(Player1.SendAsync(packet), Player2.SendAsync(packet));
    }
}

public class Prompt
{
    public required string Text { get; init; }
    public required string Answer { get; init; }
    public required string[] Options { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AnswerStatus
{
    Unanswered,
    Correct,
    Incorrect,
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(JoinPacket), "join")]
[JsonDerivedType(typeof(StartPacket), "start")]
[JsonDerivedType(typeof(AnswerPacket), "answer")]
public class ServerboundPacket
{
}

public class JoinPacket : ServerboundPacket
{
    public required string Name { get; init; }
}

public class StartPacket : ServerboundPacket
{
    public bool PlaySolo { get; init; }
}

public class AnswerPacket : ServerboundPacket
{
    public required string Answer { get; init; }
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(JoinedPacket), "joined")]
[JsonDerivedType(typeof(MatchPacket), "match")]
[JsonDerivedType(typeof(PromptPacket), "prompt")]
[JsonDerivedType(typeof(AnswerStatusPacket), "answerStatus")]
[JsonDerivedType(typeof(MatchEndPacket), "matchEnd")]
[JsonDerivedType(typeof(LeaderboardPacket), "leaderboard")]
public class ClientboundPacket
{
}

public class JoinedPacket : ClientboundPacket
{
    public required Player Player { get; init; }
}

public class MatchPacket : ClientboundPacket
{
    public required string OtherPlayerName { get; init; }
}

public class PromptPacket : ClientboundPacket
{
    public required Prompt Prompt { get; init; }
}

public class AnswerStatusPacket : ClientboundPacket
{
    public required AnswerStatus AnswerStatus { get; init; }
}

public class MatchEndPacket : ClientboundPacket
{
    public required string WinnerName { get; init; }
}

public class LeaderboardPacket : ClientboundPacket
{
    public required Player[] Players { get; init; }
}