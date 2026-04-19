using SingTray.Shared;

namespace SingTray.Client;

public sealed class FileImportService : IDisposable
{
    public FileImportService()
    {
        Directory.CreateDirectory(AppPaths.ImportsDirectory);
    }

    public async Task<string?> PrepareImportAsync(string sourcePath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        Directory.CreateDirectory(AppPaths.ImportsDirectory);
        var fileName = Path.GetFileName(sourcePath);
        var destinationPath = Path.Combine(AppPaths.ImportsDirectory, fileName);

        await using var source = File.OpenRead(sourcePath);
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination, cancellationToken);
        return fileName;
    }

    public void Dispose()
    {
    }
}
