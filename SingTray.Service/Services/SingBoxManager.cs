using System.Diagnostics;
using System.Text;
using SingTray.Shared;
using SingTray.Shared.Constants;
using SingTray.Shared.Enums;
using SingTray.Shared.Models;

namespace SingTray.Service.Services;

public sealed class SingBoxManager
{
    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly ServiceState _serviceState;
    private readonly LogService _logService;
    private Process? _currentProcess;
    private string? _lastStdErrLine;
    private bool _stopRequested;

    public SingBoxManager(ServiceState serviceState, LogService logService)
    {
        _serviceState = serviceState;
        _logService = logService;
    }

    public async Task<CoreInfo> GetCoreInfoAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(AppPaths.SingBoxExecutablePath))
        {
            return new CoreInfo
            {
                Installed = false,
                Valid = false,
                ValidationMessage = "Core not installed"
            };
        }

        var versionResult = await GetVersionFromExecutableAsync(AppPaths.SingBoxExecutablePath, cancellationToken);
        return versionResult.Success
            ? new CoreInfo { Installed = true, Valid = true, Version = versionResult.Message, ValidationMessage = "OK" }
            : new CoreInfo { Installed = true, Valid = false, ValidationMessage = versionResult.Message };
    }

    public Task<ConfigInfo> GetConfigInfoAsync(CancellationToken cancellationToken) =>
        ValidateConfigFileAsync(AppPaths.ActiveConfigPath, cancellationToken);

    public Task<ConfigInfo> ValidateConfigFileAsync(string configPath, CancellationToken cancellationToken) =>
        ValidateConfigPathAsync(configPath, cancellationToken);

    public async Task<OperationResult> StartAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_currentProcess is { HasExited: false })
            {
                return OperationResult.Ok("sing-box is already running.");
            }

            var core = await GetCoreInfoAsync(cancellationToken);
            if (!core.Installed)
            {
                return await FailStartAsync("Core not installed", cancellationToken);
            }

            if (!core.Valid)
            {
                return await FailStartAsync(core.ValidationMessage ?? "Core invalid", cancellationToken);
            }

            var config = await GetConfigInfoAsync(cancellationToken);
            if (!config.Installed)
            {
                return await FailStartAsync("Config not installed", cancellationToken);
            }

            if (!config.Valid)
            {
                return await FailStartAsync(config.ValidationMessage ?? "Config invalid", cancellationToken);
            }

            await _serviceState.UpdateAsync(record =>
            {
                record.RunState = RunState.Starting;
                record.LastError = null;
            }, cancellationToken);

            _lastStdErrLine = null;
            var process = BuildProcess(AppPaths.SingBoxExecutablePath, SingBoxConstants.RunArguments, AppPaths.CoreDirectory);
            process.EnableRaisingEvents = true;
            process.OutputDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    _ = _logService.WriteSingBoxOutputAsync("stdout", args.Data, CancellationToken.None);
                }
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    _lastStdErrLine = args.Data;
                    _ = _logService.WriteSingBoxOutputAsync("stderr", args.Data, CancellationToken.None);
                }
            };
            process.Exited += async (_, _) => await OnProcessExitedAsync(process);

            if (!process.Start())
            {
                return await FailStartAsync("Failed to launch sing-box.", cancellationToken);
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            _currentProcess = process;

            await Task.Delay(SingBoxConstants.StartProbeDelayMilliseconds, cancellationToken);

            if (process.HasExited)
            {
                var exitCode = process.ExitCode;
                var error = BuildExitMessage(exitCode, _lastStdErrLine);
                return await FailStartAsync(error, cancellationToken);
            }

            await _serviceState.UpdateAsync(record =>
            {
                record.RunState = RunState.Running;
                record.SingBoxPid = process.Id;
                record.LastError = null;
            }, cancellationToken);

            await _logService.WriteInfoAsync($"sing-box started with PID {process.Id}.", cancellationToken);
            return OperationResult.Ok("sing-box started.");
        }
        catch (Exception ex)
        {
            return await FailStartAsync($"Failed to start sing-box: {ex.Message}", cancellationToken, ex);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<OperationResult> StopAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_currentProcess is null || _currentProcess.HasExited)
            {
                await _serviceState.UpdateAsync(record =>
                {
                    record.RunState = RunState.Stopped;
                    record.SingBoxPid = null;
                }, cancellationToken);
                return OperationResult.Ok("sing-box is already stopped.");
            }

            await _serviceState.UpdateAsync(record => record.RunState = RunState.Stopping, cancellationToken);
            await _logService.WriteInfoAsync($"Stopping sing-box PID {_currentProcess.Id}.", cancellationToken);
            _stopRequested = true;

            if (_currentProcess.MainWindowHandle != IntPtr.Zero)
            {
                _currentProcess.CloseMainWindow();
            }

            if (!await WaitForExitAsync(_currentProcess, SingBoxConstants.StopTimeoutMilliseconds, cancellationToken))
            {
                _currentProcess.Kill(entireProcessTree: true);
                if (!await WaitForExitAsync(_currentProcess, 2000, cancellationToken))
                {
                    return OperationResult.Fail("Failed to stop sing-box within timeout.");
                }
            }

            await _serviceState.UpdateAsync(record =>
            {
                record.RunState = RunState.Stopped;
                record.SingBoxPid = null;
                record.LastError = null;
            }, cancellationToken);

            _currentProcess.Dispose();
            _currentProcess = null;
            return OperationResult.Ok("sing-box stopped.");
        }
        catch (Exception ex)
        {
            await _logService.WriteErrorAsync("Stop sing-box failed.", ex, cancellationToken);
            await _serviceState.UpdateAsync(record =>
            {
                record.RunState = RunState.Error;
                record.LastError = ex.Message;
            }, cancellationToken);
            return OperationResult.Fail($"Failed to stop sing-box: {ex.Message}");
        }
        finally
        {
            _stopRequested = false;
            _gate.Release();
        }
    }

    public async Task<OperationResult> RestartAsync(CancellationToken cancellationToken)
    {
        var stop = await StopAsync(cancellationToken);
        if (!stop.Success)
        {
            return stop;
        }

        return await StartAsync(cancellationToken);
    }

    private async Task<ConfigInfo> ValidateConfigPathAsync(string configPath, CancellationToken cancellationToken)
    {
        if (!File.Exists(configPath))
        {
            return new ConfigInfo
            {
                Installed = false,
                Valid = false,
                ValidationMessage = "Config not installed"
            };
        }

        try
        {
            await using var stream = File.OpenRead(configPath);
            await System.Text.Json.JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            return new ConfigInfo
            {
                Installed = true,
                Valid = false,
                FileName = Path.GetFileName(configPath),
                ValidationMessage = $"Config invalid: {ex.Message}"
            };
        }

        if (File.Exists(AppPaths.SingBoxExecutablePath))
        {
            var checkArgs = SingBoxConstants.CheckArguments.Concat([configPath]).ToArray();
            var result = await RunProcessCaptureAsync(AppPaths.SingBoxExecutablePath, checkArgs, AppPaths.CoreDirectory, cancellationToken);
            if (!result.Success)
            {
                return new ConfigInfo
                {
                    Installed = true,
                    Valid = false,
                    FileName = Path.GetFileName(configPath),
                    ValidationMessage = result.Message
                };
            }
        }

        return new ConfigInfo
        {
            Installed = true,
            Valid = true,
            FileName = Path.GetFileName(configPath),
            ValidationMessage = "OK"
        };
    }

    public async Task<OperationResult> ValidateCoreExecutableAsync(string executablePath, CancellationToken cancellationToken)
    {
        if (!File.Exists(executablePath))
        {
            return OperationResult.Fail("Core not installed");
        }

        return await GetVersionFromExecutableAsync(executablePath, cancellationToken);
    }

    public async Task<StatusInfo> GetStatusAsync(CancellationToken cancellationToken)
    {
        var core = await GetCoreInfoAsync(cancellationToken);
        var config = await GetConfigInfoAsync(cancellationToken);

        await _serviceState.UpdateAsync(record =>
        {
            record.CoreVersion = core.Version;
            record.ConfigName = config.FileName;
            if (_currentProcess is null || _currentProcess.HasExited)
            {
                if (record.RunState is RunState.Running or RunState.Starting or RunState.Stopping)
                {
                    record.RunState = RunState.Stopped;
                    record.SingBoxPid = null;
                }
            }
        }, cancellationToken);

        return await _serviceState.CreateStatusSnapshotAsync(core, config, cancellationToken);
    }

    private async Task<OperationResult> GetVersionFromExecutableAsync(string executablePath, CancellationToken cancellationToken)
    {
        var result = await RunProcessCaptureAsync(executablePath, SingBoxConstants.VersionArguments, Path.GetDirectoryName(executablePath)!, cancellationToken);
        if (!result.Success)
        {
            return OperationResult.Fail(result.Message);
        }

        var versionLine = result.Message
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault(line => line.Contains("sing-box", StringComparison.OrdinalIgnoreCase))
            ?? result.Message.Trim();

        return OperationResult.Ok(versionLine);
    }

    private static Process BuildProcess(string fileName, IEnumerable<string> arguments, string workingDirectory)
    {
        return new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                WorkingDirectory = workingDirectory,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                ArgumentList = { }
            }
        }.Also(process =>
        {
            foreach (var argument in arguments)
            {
                process.StartInfo.ArgumentList.Add(argument);
            }
        });
    }

    private async Task<OperationResult> RunProcessCaptureAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = BuildProcess(fileName, arguments, workingDirectory);
            var stdout = new StringBuilder();
            var stderr = new StringBuilder();
            process.OutputDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    stdout.AppendLine(args.Data);
                }
            };
            process.ErrorDataReceived += (_, args) =>
            {
                if (!string.IsNullOrWhiteSpace(args.Data))
                {
                    stderr.AppendLine(args.Data);
                }
            };

            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                var error = BuildExitMessage(process.ExitCode, stderr.ToString().Trim());
                return OperationResult.Fail(error);
            }

            return OperationResult.Ok(stdout.ToString().Trim());
        }
        catch (Exception ex)
        {
            return OperationResult.Fail(ex.Message);
        }
    }

    private async Task OnProcessExitedAsync(Process process)
    {
        var exitCode = process.ExitCode;
        var isIntentionalStop = _stopRequested;
        var message = exitCode == 0 || isIntentionalStop ? null : BuildExitMessage(exitCode, _lastStdErrLine);

        await _logService.WriteInfoAsync($"sing-box exited with code {exitCode}.", CancellationToken.None);
        await _serviceState.UpdateAsync(record =>
        {
            record.SingBoxPid = null;
            record.RunState = exitCode == 0 || isIntentionalStop ? RunState.Stopped : RunState.Error;
            record.LastError = message;
        }, CancellationToken.None);
    }

    private async Task<OperationResult> FailStartAsync(string message, CancellationToken cancellationToken, Exception? ex = null)
    {
        await _logService.WriteErrorAsync(message, ex, cancellationToken);
        await _serviceState.UpdateAsync(record =>
        {
            record.RunState = RunState.Error;
            record.LastError = message;
            record.SingBoxPid = null;
        }, cancellationToken);

        if (_currentProcess is not null)
        {
            _currentProcess.Dispose();
            _currentProcess = null;
        }

        return OperationResult.Fail(message);
    }

    private static string BuildExitMessage(int exitCode, string? stderr)
    {
        return string.IsNullOrWhiteSpace(stderr)
            ? $"sing-box exited with code {exitCode}."
            : $"sing-box exited with code {exitCode}: {stderr.Trim()}";
    }

    private static async Task<bool> WaitForExitAsync(Process process, int milliseconds, CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(milliseconds);
        try
        {
            await process.WaitForExitAsync(timeoutCts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return process.HasExited;
        }
    }
}

internal static class FunctionalExtensions
{
    public static T Also<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }
}
