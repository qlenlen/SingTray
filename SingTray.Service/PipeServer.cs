using System.IO.Pipes;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using SingTray.Service.Services;
using SingTray.Shared;

namespace SingTray.Service;

public sealed class PipeServer : BackgroundService
{
    private readonly PipeCommandHandler _handler;
    private readonly LogService _logService;

    public PipeServer(PipeCommandHandler handler, LogService logService)
    {
        _handler = handler;
        _logService = logService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await _logService.WriteInfoAsync($"Named pipe server starting on '{AppPaths.PipeName}'.", stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            NamedPipeServerStream? server = null;
            try
            {
                server = CreateServer();
                await _logService.WriteInfoAsync($"Named pipe waiting for connection on '{AppPaths.PipeName}'.", stoppingToken);
                await server.WaitForConnectionAsync(stoppingToken);
                await _logService.WriteInfoAsync("Named pipe client connected.", stoppingToken);

                var connectedServer = server;
                _ = Task.Run(
                    async () => await HandleConnectionAsync(connectedServer, stoppingToken),
                    stoppingToken);

                server = null;
            }
            catch (OperationCanceledException)
            {
                server?.Dispose();
            }
            catch (Exception ex)
            {
                server?.Dispose();
                await _logService.WriteErrorAsync("Named pipe accept failed.", ex, stoppingToken);
                await Task.Delay(1000, stoppingToken);
            }
        }
    }

    private async Task HandleConnectionAsync(NamedPipeServerStream server, CancellationToken cancellationToken)
    {
        if (server is null)
        {
            await _logService.WriteErrorAsync("Named pipe connection handler received a null server stream.", null, cancellationToken);
            return;
        }

        await using var stream = server;
        try
        {
            using var reader = new StreamReader(
                stream,
                Encoding.UTF8,
                detectEncodingFromByteOrderMarks: false,
                bufferSize: 1024,
                leaveOpen: true);
            using var writer = new StreamWriter(
                stream,
                new UTF8Encoding(false),
                bufferSize: 1024,
                leaveOpen: true)
            {
                AutoFlush = true
            };

            var requestJson = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(requestJson))
            {
                await _logService.WriteWarningAsync("Named pipe request was empty.", cancellationToken);
                await writer.WriteLineAsync(JsonSerializer.Serialize(PipeResponse.FromError("Empty request."), PipeContracts.JsonOptions));
                return;
            }

            var request = JsonSerializer.Deserialize<PipeRequest>(requestJson, PipeContracts.JsonOptions);
            if (request is null)
            {
                await _logService.WriteWarningAsync("Named pipe request JSON could not be deserialized.", cancellationToken);
                await writer.WriteLineAsync(JsonSerializer.Serialize(PipeResponse.FromError("Invalid request."), PipeContracts.JsonOptions));
                return;
            }

            await _logService.WriteInfoAsync($"Named pipe request received: {request.Action}", cancellationToken);
            var response = await _handler.HandleAsync(request, cancellationToken);
            await writer.WriteLineAsync(JsonSerializer.Serialize(response, PipeContracts.JsonOptions));
            await _logService.WriteInfoAsync($"Named pipe request handled: {request.Action}, success={response.Success}.", cancellationToken);
        }
        catch (Exception ex)
        {
            await _logService.WriteErrorAsync("Named pipe connection failed.", ex, cancellationToken);
        }
    }

    private static NamedPipeServerStream CreateServer()
    {
        var security = CreatePipeSecurity();
        return NamedPipeServerStreamAcl.Create(
            AppPaths.PipeName,
            PipeDirection.InOut,
            NamedPipeServerStream.MaxAllowedServerInstances,
            PipeTransmissionMode.Byte,
            PipeOptions.Asynchronous,
            4096,
            4096,
            security,
            HandleInheritability.None,
            PipeAccessRights.ChangePermissions);
    }

    private static PipeSecurity CreatePipeSecurity()
    {
        var security = new PipeSecurity();
        var localSystem = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
        var builtInAdmins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
        var authenticatedUsers = new SecurityIdentifier(WellKnownSidType.AuthenticatedUserSid, null);
        var users = new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null);

        security.AddAccessRule(new PipeAccessRule(localSystem, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(builtInAdmins, PipeAccessRights.FullControl, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(authenticatedUsers, PipeAccessRights.ReadWrite | PipeAccessRights.CreateNewInstance, AccessControlType.Allow));
        security.AddAccessRule(new PipeAccessRule(users, PipeAccessRights.ReadWrite, AccessControlType.Allow));
        return security;
    }
}
