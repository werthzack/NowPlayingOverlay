using Windows.Media.Control;

Console.WriteLine("Now Playing detector starting...");

var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();

string lastTitle = "";

while (true)
{
    var session = manager.GetCurrentSession();
    var playbackInfo = session.GetPlaybackInfo();

    if (playbackInfo.PlaybackStatus == GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing)
    {
        var props = await session.TryGetMediaPropertiesAsync();
        string title = props.Title;
        string artist = props.Artist;
        
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