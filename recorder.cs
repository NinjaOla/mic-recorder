#:package Spectre.Console@0.54.0
#:package NAudio@2.2.1
#:package Spectre.Console.Cli@0.53.1

using NAudio.Wave;
using Spectre.Console;

AnsiConsole.Write(new FigletText("Mic Recorder").Color(Color.Aqua));

// --- List & select microphone ---
var devices = new List<MicDevice>();
for (var i = 0; i < WaveInEvent.DeviceCount; i++)
{
    var caps = WaveInEvent.GetCapabilities(i);
    devices.Add(new MicDevice(i, caps.ProductName, caps.Channels));
}

if (devices.Count == 0)
{
    AnsiConsole.MarkupLine("[red]No microphone devices found.[/]");
    return;
}

var selected = AnsiConsole.Prompt(
    new SelectionPrompt<MicDevice>()
        .Title("[green]Select a microphone:[/]")
        .UseConverter(d => Markup.Escape(d.DisplayName))
        .AddChoices(devices));

AnsiConsole.MarkupLine($"[bold]Selected:[/] [cyan]{Markup.Escape(selected.DisplayName)}[/]");

// --- Configure recording ---
var sampleRate = AnsiConsole.Prompt(
    new SelectionPrompt<int>()
        .Title("[green]Sample rate:[/]")
        .AddChoices(16_000, 44_100, 48_000)
        .UseConverter(r => $"{r} Hz"));

var channels = AnsiConsole.Prompt(
    new SelectionPrompt<int>()
        .Title("[green]Channels:[/]")
        .AddChoices(1, 2)
        .UseConverter(c => c == 1 ? "Mono" : "Stereo"));

// --- Record ---
var outputPath = Path.Combine(Environment.CurrentDirectory,
    $"recording_{DateTime.Now:yyyyMMdd_HHmmss}.wav");

using var waveIn = new WaveInEvent
{
    DeviceNumber = selected.DeviceNumber,
    WaveFormat = new WaveFormat(sampleRate, 16, channels),
    BufferMilliseconds = 50
};

using var writer = new WaveFileWriter(outputPath, waveIn.WaveFormat);

long bytesRecorded = 0;

waveIn.DataAvailable += (_, e) =>
{
    writer.Write(e.Buffer, 0, e.BytesRecorded);
    bytesRecorded += e.BytesRecorded;
};

waveIn.StartRecording();

AnsiConsole.Status()
    .Spinner(Spinner.Known.Dots)
    .SpinnerStyle(Style.Parse("red"))
    .Start("[red]● Recording...[/] Press [bold]Enter[/] to stop", _ =>
    {
        while (!Console.KeyAvailable || Console.ReadKey(true).Key != ConsoleKey.Enter)
        {
            Thread.Sleep(100);
        }
    });

waveIn.StopRecording();
writer.Flush();
writer.Dispose();

var duration = TimeSpan.FromSeconds((double)bytesRecorded / waveIn.WaveFormat.AverageBytesPerSecond);

AnsiConsole.MarkupLine($"[dim]Recorded {duration:mm\\:ss\\.ff} ({bytesRecorded / 1024.0:F1} KB)[/]");
AnsiConsole.MarkupLine($"[green]Saved to:[/] [link]{Markup.Escape(outputPath)}[/]");



if (AnsiConsole.Confirm("[yellow]Play back recording?[/]"))
{

    // var outputDevices = new List<OutputDevice>();
    // for (var i = 0; i < WaveOut.DeviceCount; i++)
    // {
    //     var caps = WaveOut.GetCapabilities(i);
    //     outputDevices.Add(new OutputDevice(i, caps.ProductName, caps.Channels));
    // }

    // if (outputDevices.Count == 0)
    // {
    //     AnsiConsole.MarkupLine("[red]No output devices found.[/]");
    //     return;
    // }

    // var selectedOutput = AnsiConsole.Prompt(
    //     new SelectionPrompt<OutputDevice>()
    //         .Title("[green]Select output device:[/]")
    //         .UseConverter(d => Markup.Escape(d.DisplayName))
    //         .AddChoices(outputDevices));

    using var audioFile = new AudioFileReader(outputPath);
    using var waveOut = new WaveOutEvent();
    waveOut.Init(audioFile);
    waveOut.Play();

    AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .SpinnerStyle(Style.Parse("green"))
        .Start("[green]▶ Playing...[/] Press [bold]Enter[/] to stop", _ =>
        {
            while (waveOut.PlaybackState == PlaybackState.Playing)
            {
                if (Console.KeyAvailable && Console.ReadKey(true).Key == ConsoleKey.Enter)
                {
                    waveOut.Stop();
                    break;
                }
                Thread.Sleep(100);
            }
        });

    AnsiConsole.MarkupLine("[dim]Playback finished.[/]");
}
// --- Types ---
record MicDevice(int DeviceNumber, string ProductName, int Channels)
{
    public string DisplayName => $"{ProductName} ({Channels}ch) [Device {DeviceNumber}]";
}

record OutputDevice(int DeviceNumber, string ProductName, int Channels)
{
    public string DisplayName => $"{ProductName} ({Channels}ch) [Device {DeviceNumber}]";
}