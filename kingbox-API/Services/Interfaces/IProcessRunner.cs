namespace KingBox.Api.Services.Interfaces;

/// <summary>
/// Execution options for running an external process safely.
/// </summary>
public record ProcessExecutionOptions(
    string ExecutablePath,
    IEnumerable<string> Arguments,
    string? WorkingDirectory = null,
    TimeSpan? Timeout = null,
    Action<string>? OnOutputLine = null,
    Action<string>? OnErrorLine = null,
    CancellationToken CancellationToken = default
);

/// <summary>
/// Result of an external process execution.
/// </summary>
public record ProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError,
    bool IsSuccess,
    bool IsCancelled,
    bool IsTimedOut
);

/// <summary>
/// Safe external process runner abstraction with argument lists and process-tree cancellation.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Executes a process with structured arguments, streaming callbacks, timeout, and cancellation.
    /// </summary>
    Task<ProcessResult> RunAsync(ProcessExecutionOptions options);
}
