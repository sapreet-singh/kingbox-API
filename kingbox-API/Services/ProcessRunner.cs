using System.Diagnostics;
using System.Text;
using KingBox.Api.Services.Interfaces;

namespace KingBox.Api.Services;

/// <summary>
/// Thread-safe process runner with asynchronous output streaming, argument isolation, and tree termination.
/// </summary>
public class ProcessRunner : IProcessRunner
{
    private readonly ILogger<ProcessRunner> _logger;

    public ProcessRunner(ILogger<ProcessRunner> logger)
    {
        _logger = logger;
    }

    public async Task<ProcessResult> RunAsync(ProcessExecutionOptions options)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = options.ExecutablePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        if (!string.IsNullOrWhiteSpace(options.WorkingDirectory))
        {
            startInfo.WorkingDirectory = options.WorkingDirectory;
        }

        foreach (var arg in options.Arguments)
        {
            startInfo.ArgumentList.Add(arg);
        }

        var stdoutBuffer = new StringBuilder();
        var stderrBuffer = new StringBuilder();
        var isTimedOut = false;
        var isCancelled = false;

        using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

        using var timeoutCts = options.Timeout.HasValue
            ? new CancellationTokenSource(options.Timeout.Value)
            : new CancellationTokenSource();

        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            options.CancellationToken,
            timeoutCts.Token);

        var processExitTcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stdoutBuffer.AppendLine(e.Data);
                try
                {
                    options.OnOutputLine?.Invoke(e.Data);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error in process stdout line handler.");
                }
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                stderrBuffer.AppendLine(e.Data);
                try
                {
                    options.OnErrorLine?.Invoke(e.Data);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Error in process stderr line handler.");
                }
            }
        };

        process.Exited += (_, _) =>
        {
            processExitTcs.TrySetResult(process.ExitCode);
        };

        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start process: {options.ExecutablePath}");
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using (linkedCts.Token.Register(() =>
            {
                if (options.CancellationToken.IsCancellationRequested)
                {
                    isCancelled = true;
                    _logger.LogInformation("Cancellation requested for process {FileName} (PID: {Pid}). Terminating process tree...", options.ExecutablePath, process.Id);
                }
                else if (timeoutCts.IsCancellationRequested)
                {
                    isTimedOut = true;
                    _logger.LogWarning("Process {FileName} (PID: {Pid}) exceeded timeout {Timeout}. Terminating process tree...", options.ExecutablePath, process.Id, options.Timeout);
                }

                KillProcessTreeSafely(process);
                processExitTcs.TrySetCanceled();
            }))
            {
                await processExitTcs.Task.ConfigureAwait(false);
            }

            // Wait for remaining asynchronous output to flush
            await process.WaitForExitAsync().ConfigureAwait(false);

            var exitCode = process.ExitCode;
            return new ProcessResult(
                ExitCode: exitCode,
                StandardOutput: stdoutBuffer.ToString(),
                StandardError: stderrBuffer.ToString(),
                IsSuccess: exitCode == 0 && !isCancelled && !isTimedOut,
                IsCancelled: isCancelled,
                IsTimedOut: isTimedOut
            );
        }
        catch (OperationCanceledException)
        {
            KillProcessTreeSafely(process);
            return new ProcessResult(
                ExitCode: -1,
                StandardOutput: stdoutBuffer.ToString(),
                StandardError: stderrBuffer.ToString(),
                IsSuccess: false,
                IsCancelled: isCancelled || options.CancellationToken.IsCancellationRequested,
                IsTimedOut: isTimedOut || (options.Timeout.HasValue && timeoutCts.IsCancellationRequested)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception running external process: {ExecutablePath}", options.ExecutablePath);
            KillProcessTreeSafely(process);
            throw;
        }
    }

    private void KillProcessTreeSafely(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                _logger.LogDebug("Killed process tree for PID {Pid}", process.Id);
            }
        }
        catch (InvalidOperationException)
        {
            // Process already exited
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to kill process tree for PID {Pid}", process.Id);
        }
    }
}
