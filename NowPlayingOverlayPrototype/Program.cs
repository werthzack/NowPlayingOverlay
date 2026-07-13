using System.Net;
using System.Runtime.InteropServices.JavaScript;
using System.Text;
using System.Text.Json;
using Windows.Media.Control;

Console.WriteLine("Now Playing detector starting...");

var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

var playingData = new Dictionary<String, object>()
{
    { "title", "" },
    { "artist", "" },
    { "position", ""},
    { "duration", ""},
};

var listener = new HttpListener();
listener.Prefixes.Add("http://localhost:8080/");
listener.Start();
Console.WriteLine("Listening at http://localhost:8080/");

_ = Task.Run(async () =>
{
    while (true)
    {
        var context = await listener.GetContextAsync();
        var request = context.Request;
        var response = context.Response;
        
        if (request.Url!.AbsolutePath == "/nowplaying.json")
        {
            var data = JsonSerializer.Serialize(playingData);
            var buffer = Encoding.UTF8.GetBytes(data);
            response.ContentType = "application/json";
            response.ContentLength64 = buffer.Length;
            await response.OutputStream.WriteAsync(buffer);
        }
        else if (request.Url.AbsolutePath == "/overlay")
        {
            try
            {
                var htmlPath = Path.Combine(AppContext.BaseDirectory, "overlay.html");
                var html = File.ReadAllText(htmlPath);
                var buffer = Encoding.UTF8.GetBytes(html);
                response.ContentType = "text/html";
                response.ContentLength64 = buffer.Length;
                await response.OutputStream.WriteAsync(buffer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Overlay error: {ex.Message}");
                response.StatusCode = 500;
            }

        }
        
        response.Close();
    }
});

string lastTitle = "";

while (true)
{
    var session = manager.GetCurrentSession();

    if (session == null)
    {
        if (lastTitle != "")
        {
            Console.WriteLine("Nothing playing.");
            lastTitle = "";
            playingData["title"] = "";
            playingData["artist"] = "";
        }
        await Task.Delay(1000);
        continue;
    }

    string appId = session.SourceAppUserModelId;

    if (!appId.Contains("Chrome", StringComparison.OrdinalIgnoreCase) &&
        !appId.Contains("Edge", StringComparison.OrdinalIgnoreCase))
    {
        if (lastTitle != "Not Sourced")
        {
            Console.WriteLine("Not sourcing from a browser");
            lastTitle = "Not Sourced";
            playingData["title"] = "";
            playingData["artist"] = "";
        }
        await Task.Delay(1000);
        continue;
    }

    var playbackInfo = session.GetPlaybackInfo();

    if (playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
    {
        var props = await session.TryGetMediaPropertiesAsync();
        var timeline = session.GetTimelineProperties();

        playingData["title"] = props.Title;
        playingData["artist"] = props.Artist;
        playingData["position"] = timeline.Position.TotalSeconds;
        playingData["duration"] = timeline.EndTime.TotalSeconds;

        if (props.Title != lastTitle)
        {
            Console.WriteLine($"Now playing: {props.Title} - {props.Artist}");
            lastTitle = props.Title;
        }
    }
    else
    {
        if (lastTitle != "")
        {
            Console.WriteLine("Nothing playing.");
            lastTitle = "";
            playingData["title"] = "";
            playingData["artist"] = "";
        }
    }

    await Task.Delay(1000);
}