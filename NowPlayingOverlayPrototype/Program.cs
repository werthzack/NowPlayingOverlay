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

byte[]? currentThumbnail = null;

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
        else if (request.Url.AbsolutePath == "/thumbnail.jpg")
        {
            if (currentThumbnail != null)
            {
                response.ContentType = "image/jpeg";
                response.ContentLength64 = currentThumbnail.Length;
                await response.OutputStream.WriteAsync(currentThumbnail);
            }
            else
            {
                response.StatusCode = 404;
            }
        }
        
        response.Close();
    }
});

GlobalSystemMediaTransportControlsSession? currentSession = null;

void SetSubscription(GlobalSystemMediaTransportControlsSession session, bool subscribe)
{
    if (subscribe)
    {
        session.MediaPropertiesChanged += OnMediaPropertiesChanged;
        session.PlaybackInfoChanged += OnPlaybackInfoChanged;
        session.TimelinePropertiesChanged += OnTimelineChanged;
    }
    else
    {
        session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
        session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        session.TimelinePropertiesChanged -= OnTimelineChanged;
    }
}

async void AttachToSession(GlobalSystemMediaTransportControlsSession? session)
{
    try
    {
        if (currentSession != null)
            SetSubscription(currentSession, subscribe: false);

        currentSession = session;

        if (currentSession != null)
        {
            SetSubscription(currentSession, subscribe: true);
            await UpdateAllData(currentSession);
        }
        else
        {
            ClearPlayingData();
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"AttachToSession error: {ex.Message}");
    }
}
async void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession session, MediaPropertiesChangedEventArgs args)
    => await UpdateAllData(session);

async void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession session, PlaybackInfoChangedEventArgs args)
    => await UpdateAllData(session);

async void OnTimelineChanged(GlobalSystemMediaTransportControlsSession session, TimelinePropertiesChangedEventArgs args)
    => await UpdateAllData(session);

async Task UpdateAllData(GlobalSystemMediaTransportControlsSession session)
{
    var playbackInfo = session.GetPlaybackInfo();
    if (playbackInfo.PlaybackStatus != GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
    {
        ClearPlayingData();
        return;
    }

    var props = await session.TryGetMediaPropertiesAsync();
    var timeline = session.GetTimelineProperties();

    playingData["title"] = props.Title;
    playingData["artist"] = props.Artist;
    playingData["position"] = timeline.Position.TotalSeconds;
    Console.WriteLine(timeline.Position.TotalSeconds);
    playingData["duration"] = timeline.EndTime.TotalSeconds;
    
    if (props.Thumbnail != null)
    {
        using var stream = await props.Thumbnail.OpenReadAsync();
        var bytes = new byte[stream.Size];
        using var reader = new Windows.Storage.Streams.DataReader(stream);
        await reader.LoadAsync((uint)stream.Size);
        reader.ReadBytes(bytes);
        currentThumbnail = bytes;
    }
    else
    {
        currentThumbnail = null;
    }
}

void ClearPlayingData()
{
    playingData["title"] = "";
    playingData["artist"] = "";
}

manager.CurrentSessionChanged += async (s, e) => AttachToSession(manager.GetCurrentSession());
AttachToSession(manager.GetCurrentSession());

Console.WriteLine("Press Ctrl+C to exit.");
await Task.Delay(-1);