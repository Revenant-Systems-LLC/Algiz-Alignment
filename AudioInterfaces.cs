namespace SageRage.Infrastructure;

public interface ILiveAudioProvider
{
    Task<ILiveAudioSession> ConnectAsync(string model, LiveConfig config, CancellationToken ct = default);
}

public interface ILiveAudioSession : IAsyncDisposable
{
    Task SendAudioAsync(byte[] pcm16kChunk, CancellationToken ct = default);
    Task SendTextAsync(string text, bool endOfTurn = true, CancellationToken ct = default);
    IAsyncEnumerable<LiveEvent> ReceiveAsync(CancellationToken ct = default);
}

public record LiveConfig(
    string[] ResponseModalities,
    string SystemInstruction);

public abstract record LiveEvent;
public record SetupComplete() : LiveEvent;
public record InputTranscription(string Text) : LiveEvent;
public record OutputTranscription(string Text) : LiveEvent;
public record OutputAudioChunk(byte[] Data) : LiveEvent;
public record TurnComplete() : LiveEvent;