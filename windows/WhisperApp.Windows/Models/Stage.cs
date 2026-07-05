namespace WhisperApp.Models;

/// Visual processing stage — drives the floating status overlay and tray tooltip.
public enum StageKind { Idle, Recording, Transcribing, Correcting, Done, Error }

public readonly struct Stage : IEquatable<Stage>
{
    public StageKind Kind { get; }
    public string Message { get; }

    private Stage(StageKind kind, string message = "")
    {
        Kind = kind;
        Message = message;
    }

    public static readonly Stage Idle = new(StageKind.Idle);
    public static readonly Stage Recording = new(StageKind.Recording);
    public static readonly Stage Transcribing = new(StageKind.Transcribing);
    public static readonly Stage Correcting = new(StageKind.Correcting);

    public static Stage Done(string snippet) => new(StageKind.Done, snippet);
    public static Stage Error(string message) => new(StageKind.Error, message);

    public bool Equals(Stage other) => Kind == other.Kind && Message == other.Message;
    public override bool Equals(object? obj) => obj is Stage other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(Kind, Message);
    public static bool operator ==(Stage a, Stage b) => a.Equals(b);
    public static bool operator !=(Stage a, Stage b) => !a.Equals(b);
}
