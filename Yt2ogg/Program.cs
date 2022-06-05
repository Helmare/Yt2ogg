using Yt2ogg;
using YoutubeExplode;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;
using System.CommandLine;
using Pastel;
using System.Diagnostics;
using System.Drawing;
using Avyn.Media;
using YoutubeExplode.Common;
using System.Drawing.Imaging;
using System.IO;

public class Program
{
    public static readonly Option<string> VideoURL = new Option<string>(new[] { "-v", "--video" })
    {
        IsRequired = true,
        ArgumentHelpName = "url",
    };
    public static readonly Option<bool> DownloadCoverOpt = new Option<bool>(new[] { "-c", "--cover" });
    public static readonly Option<bool> DebugFFmpeg = new Option<bool>("--debug-ffmpeg");
    public static Argument<FileInfo> OutputPath = new Argument<FileInfo>
    {
        Arity = ArgumentArity.ExactlyOne
    }.LegalFilePathsOnly();

    public static async Task Run(string url, bool downloadCover, bool debugFFmpeg, FileInfo output)
    {
        YoutubeClient client = new YoutubeClient();
        VideoId? id = VideoId.TryParse(url);
        if (id == null)
        {
            Console.WriteLine("Invalid Youtube URL or ID".Pastel("#FF0000"));
            Environment.ExitCode = -1;
            return;
        }

        Video? video = null;
        StreamManifest? streams = null;

        Console.Write("Obtaining video information... ");
        ConsoleAnimator.Working();
        try
        {
            video = await client.Videos.GetAsync(id.Value);
            streams = await client.Videos.Streams.GetManifestAsync(id.Value);
        }
        catch (Exception ex)
        {
            ConsoleAnimator.Stop();
            Console.WriteLine("Failed to obtain video information".Pastel("#FF0000"));
            Console.WriteLine(ex.Message.Pastel("#FF0000"));
            Environment.ExitCode = -1;
            return;
        }
        ConsoleAnimator.Stop();

        bool success = await DownloadVideo(client, video, streams, debugFFmpeg, output);
        if (!success)
        {
            Console.WriteLine("\nFailed to convert video.".Pastel("#FF0000"));
            Environment.ExitCode = -1;
            return;
        }

        if (downloadCover)
        {
            success = await DownloadCover(video, output, debugFFmpeg);
            if (!success)
            {
                Console.WriteLine("\nFailed to convert video.".Pastel("#FF0000"));
                Environment.ExitCode = -1;
                return;
            }
        }

        Console.WriteLine("\nFinished!".Pastel("#00FF00"));
    }

    public static async Task<bool> DownloadVideo(YoutubeClient client, Video video, StreamManifest streams, bool debugFFmpeg, FileInfo output)
    {
        IStreamInfo stream = streams.GetAudioOnlyStreams().GetWithHighestBitrate();

        Console.Write($"Downloading {video.Title.Pastel("#FFFF00")} [{stream.Bitrate}]");
        ConsoleAnimator.Working();

        string tempFile = Path.Combine(output.DirectoryName, "temp." + stream.Container.Name);
        await client.Videos.Streams.DownloadAsync(stream, tempFile);
        ConsoleAnimator.Stop();

        return ConvertFile(tempFile, output.FullName, debugFFmpeg);
    }
    public static async Task<bool> DownloadCover(Video video, FileInfo output, bool debugFFmpeg)
    {
        Thumbnail thumbnail = video.Thumbnails[0];
        foreach (Thumbnail t in video.Thumbnails)
        {
            if (t.Resolution.Area > thumbnail.Resolution.Area)
            {
                thumbnail = t;
            }
        }

        string tempFile = Path.Combine(output.DirectoryName, "cover-temp");
        string coverFile = Path.Combine(output.DirectoryName, "cover.png");

        Console.Write($"Downloading cover [{thumbnail.Resolution.Width}x{thumbnail.Resolution.Height}]");
        ConsoleAnimator.Working();
        using (HttpClient client = new HttpClient())
        using (Stream imageData = await client.GetStreamAsync(thumbnail.Url))
        using (FileStream file = File.OpenWrite(tempFile))
        {
            imageData.CopyTo(file);
            file.Flush();
        }
        ConsoleAnimator.Stop();

        return ConvertFile(tempFile, coverFile, debugFFmpeg);
    }
    /// <summary>
    ///     Converts a temp file to the output file and removes the temp file.
    /// </summary>
    /// <param name="tempFile"></param>
    /// <param name="outputFile"></param>
    /// <param name="debug"></param>
    /// <returns></returns>
    public static bool ConvertFile(string tempFile, string outputFile, bool debug)
    {
        Console.Write(
            $"Converting " +
            $"{new FileInfo(tempFile).Name.Pastel("#FFFF00")} to " +
            $"{new FileInfo(outputFile).Name.Pastel("#FFFF00")}"
        );
        Process ffmpeg = FFmpeg.Query("-i @{0} -y @{1}", tempFile, outputFile);

        if (debug)
        {
            Console.WriteLine();
            FFmpeg.DebugStandardError(ffmpeg, "", (data) =>
            {
                Console.WriteLine(data);
            });
            ffmpeg.WaitForExit();
        }
        else
        {
            ConsoleAnimator.Working();
            ffmpeg.WaitForExit();
            ConsoleAnimator.Stop();
        }

        File.Delete(tempFile);

        return ffmpeg.ExitCode == 0;
    }

    public static async Task Main(string[] args)
    {
        RootCommand cli = new RootCommand()
        {
            VideoURL, DownloadCoverOpt, DebugFFmpeg, OutputPath
        };
        cli.SetHandler(Run, VideoURL, DownloadCoverOpt, DebugFFmpeg, OutputPath);
        await cli.InvokeAsync(args);

        Console.WriteLine();
    }
}