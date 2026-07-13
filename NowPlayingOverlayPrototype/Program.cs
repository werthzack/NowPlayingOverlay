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
    { "startTime", ""},
    { "endTime", ""},
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
        
        response.Close();
    }
});

string lastTitle = "";

while (true)
{
    var session = manager.GetCurrentSession();
    var playbackInfo = session.GetPlaybackInfo();
    string appId = session.SourceAppUserModelId;

    if (!appId.Contains("Chrome", StringComparison.OrdinalIgnoreCase) &&
        !appId.Contains("Edge", StringComparison.OrdinalIgnoreCase))
    {
        if (lastTitle != "Not Sourced")
        {
            Console.WriteLine("Not Sourcing from a Browser");
            lastTitle = "Not Sourced";
        }

        continue;
    }
    
    if (playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
    {
        var props = await session.TryGetMediaPropertiesAsync();
        var timeline = session.GetTimelineProperties();
        
        string title = props.Title;
        string artist = props.Artist;
        

        TimeSpan position = timeline.Position;
        TimeSpan startTime = timeline.StartTime;
        TimeSpan endTime = timeline.EndTime; 
        
        playingData["title"] = title;
        playingData["artist"] = artist;
        playingData["position"] = position.ToString();
        playingData["startTime"] = startTime.ToString();
        playingData["endTime"] = endTime.ToString();
        
        if (title != lastTitle)
        {
            Console.WriteLine($"Now playing: {title} - {artist}");
            lastTitle = title;
        }
    }
    else
    {
        if (lastTitle != "")
        {
            Console.WriteLine("Nothing playing.");
            lastTitle = "";
        }
    }

    await Task.Delay(1000);
}