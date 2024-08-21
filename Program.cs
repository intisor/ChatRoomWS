using System.Net;
using System.Net.WebSockets;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var connections = new List<WebSocket>();
app.UseWebSockets();
app.MapGet("/ws",async (context) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var clientName = context.Request.Query["Name"];
        using var websocket = await context.WebSockets.AcceptWebSocketAsync();
        connections.Add(websocket);

        await BroadCast($"{clientName} joined the room");
        await BroadCast($"{connections.Count} clients connected");
        await ReceiveMessage(websocket,
            async(result,buffer) =>
            {
                if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer,0,buffer.Length);
                    await BroadCast($"{clientName} : {message}");
                }
                else if(result.MessageType == WebSocketMessageType.Close || websocket.State == WebSocketState.Aborted)
                {
                    connections.Remove(websocket);
                    await BroadCast($"{clientName} left the room");
                    await BroadCast($"{connections.Count} left");
                    await websocket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                }
            });
        //while (true)
        //{
        //    var message = $"The current time is {DateTime.Now}";
        //    var buffer = Encoding.UTF8.GetBytes(message);
        //    var arraySegment = new ArraySegment<byte>(buffer, 0, buffer.Length);
        //    if (websocket.State == WebSocketState.Open)
        //    {
        //        await websocket.SendAsync(arraySegment, WebSocketMessageType.Text, true, CancellationToken.None);
        //    }
        //    else if(websocket.State == WebSocketState.Closed || websocket.State == WebSocketState.Aborted)
        //    {
        //        break;
        //    }
        //    Thread.Sleep(1000);
        //}
    }
    else
    {
        context.Response.StatusCode = (int)HttpStatusCode.BadRequest;
    }
});

async Task ReceiveMessage(WebSocket socket, Action<WebSocketReceiveResult, Byte[]> handleMessage)
{
    var buffer = new byte[1024 * 4];
    while (socket.State == WebSocketState.Open)
    {
        var result = await socket.ReceiveAsync(new ArraySegment<byte>(buffer),CancellationToken.None);
        handleMessage(result, buffer);
    }
}
async Task BroadCast(string message)
{
    var bytes = Encoding.UTF8.GetBytes(message);
    foreach (var sockets in connections)
    {
        if (sockets.State == WebSocketState.Open)
        {
            var arraySegment = new ArraySegment<byte>(bytes, 0, bytes.Length);
            await sockets.SendAsync(arraySegment,WebSocketMessageType.Text,true,CancellationToken.None);
        }
    }
}
app.Run();
