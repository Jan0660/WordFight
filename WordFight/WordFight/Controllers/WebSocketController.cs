using System.Buffers;
using System.Net.WebSockets;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace WordFight.Controllers;

// [Route("/ws")]
public class WebSocketController : ControllerBase
{
    [Route("/ws")]
    [HttpGet]
    public async Task Ws()
    {
        if (HttpContext.WebSockets.IsWebSocketRequest)
        {
            Log73.Console.Info("WebSocket request");
            using var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
            var tcs = new TaskCompletionSource();

            // wait until join packet
            var buffer = ArrayPool<byte>.Shared.Rent(4096);
            var pack = await webSocket.ReceiveAsync(buffer, CancellationToken.None);
            if (pack.CloseStatus != null)
            {
                await webSocket.CloseAsync(pack.CloseStatus.Value, pack.CloseStatusDescription, CancellationToken.None);
                return;
            }
            var packet = JsonSerializer.Deserialize<ServerboundPacket>(buffer.AsSpan(0, pack.Count),
                Globals.JsonOptions)!;
            ArrayPool<byte>.Shared.Return(buffer);
            if (packet is JoinPacket joinPacket)
            {
                var player = new Player()
                {
                    Name = joinPacket.Name,
                    Socket = webSocket,
                    SocketTaskCompletionSource = tcs,
                };
                await player.SendAsync(new JoinedPacket()
                {
                    Player = player,
                });
                Game.NewPlayer(player);
                // await webSocket.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new JoinedPacket()
                // {
                //     // Type = "joined",
                //     Player = player,
                // }, Globals.JsonOptions)), WebSocketMessageType.Text, true, CancellationToken.None);
            }
            else
            {
                await webSocket.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "Invalid packet", CancellationToken.None);
                return;
            }
            

            await tcs.Task;
        }
        else
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        }
    }
}