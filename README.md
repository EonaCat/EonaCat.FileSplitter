# EonaCat.FileSplitter

Fast, reliable file splitting and assembly for .NET.

`EonaCat.FileSplitter` splits large files into manageable chunks, stores SHA-256 integrity information in manifests, optionally compresses chunks with GZip, and can assemble one or multiple files back into their original form.

## Features

- Split a single file into chunks.
- Split multiple files into one package.
- Assemble a single file from a package.
- Assemble every file in a package.
- Optional GZip compression for chunks.
- SHA-256 hashing for every chunk and the complete file.
- Integrity validation during assembly.
- Progress reporting with `IProgress<T>`.
- Cancellation support with `CancellationToken`.
- Safe temporary-file assembly to avoid leaving a partially written destination.
- Targets **.NET Standard 2.0**, making the library usable from a wide range of .NET applications.
- No external runtime dependencies.

## Installation

### NuGet

```bash
dotnet add package EonaCat.FileSplitter
```

## Basic usage

### Split a file

```csharp
using EonaCat.FileSplitter;
using EonaCat.FileSplitter.Models;

var splitter = new FileSplitter();

FileManifest manifest = await splitter.SplitAsync(
    sourceFile: @"C:\Files\large-file.zip",
    outputDirectory: @"C:\Packages\large-file");

Console.WriteLine($"Created {manifest.Chunks.Count} chunks.");
Console.WriteLine($"SHA-256: {manifest.OverallSha256}");
```

The package directory contains a `manifest.json` and a `chunks` directory:

```text
large-file/
├── manifest.json
├── index.json
└── chunks/
    ├── 00000000.part
    ├── 00000001.part
    └── 00000002.part
```

## Configure chunk size

The default chunk size is **64 MiB**.

```csharp
var options = new FileTransportOptions
{
    ChunkSizeBytes = 128 * 1024 * 1024, // 128 MiB
    BufferSizeBytes = 1024 * 1024       // 1 MiB I/O buffer
};

var manifest = await splitter.SplitAsync(
    @"C:\Files\large-file.zip",
    @"C:\Packages\large-file",
    options);
```

`ChunkSizeBytes` must be at least 1 KiB.

`BufferSizeBytes` must be at least 4 KiB.

## Compress chunks

Enable GZip compression with `CompressChunks`.

```csharp
var options = new FileTransportOptions
{
    ChunkSizeBytes = 64 * 1024 * 1024,
    CompressChunks = true
};

await splitter.SplitAsync(
    @"C:\Files\large-file.zip",
    @"C:\Packages\large-file",
    options);
```

Compression is useful when the source data is compressible. Already-compressed files such as ZIP, JPEG, MP4, and many installers may see little or no reduction.

The manifest records whether compression was used, so assembly automatically decompresses the chunks.

## Assemble a file

For a package containing one file, no original filename is required:

```csharp
using EonaCat.FileSplitter;

var assembler = new FileAssembler();

await assembler.AssembleAsync(
    packageDirectory: @"C:\Packages\large-file",
    destinationFile: @"C:\Restored\large-file.zip");
```

The assembler:

1. Reads `manifest.json`.
2. Reads each chunk in order.
3. Decompresses compressed chunks when required.
4. Verifies every chunk's SHA-256 hash.
5. Verifies the final file SHA-256 hash.
6. Writes the result to a temporary file.
7. Moves the completed file into its final location.

If validation fails, an `InvalidDataException` is thrown.

## Assemble a specific file from a multi-file package

When a package contains multiple files, specify the original filename:

```csharp
await assembler.AssembleAsync(
    packageDirectory: @"C:\Packages\my-package",
    destinationFile: @"C:\Restored\report.pdf",
    originalFileName: "report.pdf");
```

The filename is matched case-insensitively against the package index.

## Split multiple files

Use `SplitManyAsync` to create one package containing multiple files:

```csharp
var files = new[]
{
    @"C:\Files\report.pdf",
    @"C:\Files\data.csv",
    @"C:\Files\archive.zip"
};

List<FileManifest> manifests = await splitter.SplitManyAsync(
    files,
    @"C:\Packages\my-package");
```

The resulting package has an `index.json` at its root and one directory per file:

```text
my-package/
├── index.json
├── file_0000/
│   ├── manifest.json
│   └── chunks/
│       ├── 00000000.part
│       └── ...
├── file_0001/
│   ├── manifest.json
│   └── chunks/
│       └── ...
└── file_0002/
    ├── manifest.json
    └── chunks/
        └── ...
```

## List files in a package

Before assembling a multi-file package, you can inspect its contents:

```csharp
var files = await assembler.ListPackageFilesAsync(
    @"C:\Packages\my-package");

foreach (var file in files)
{
    Console.WriteLine($"{file.OriginalFileName} - {file.FileLength:N0} bytes");
    Console.WriteLine($"SHA-256: {file.OverallSha256}");
}
```

Each `PackageFileInfo` provides:

- `OriginalFileName`
- `RelativePath`
- `FileLength`
- `OverallSha256`

## Assemble all files

To restore every file in a package:

```csharp
List<string> restoredFiles = await assembler.AssembleAllAsync(
    packageDirectory: @"C:\Packages\my-package",
    destinationDirectory: @"C:\Restored");

foreach (string file in restoredFiles)
{
    Console.WriteLine($"Restored: {file}");
}
```

Existing files are not silently overwritten when multiple package entries have the same filename. A numeric suffix is used instead:

```text
report.pdf
report (2).pdf
report (3).pdf
```

## Progress reporting

### Single-file progress

Use `IProgress<TransferProgress>`:

```csharp
var progress = new Progress<TransferProgress>(p =>
{
    Console.WriteLine(
        $"{p.Percent:F1}% - " +
        $"{p.ProcessedBytes:N0}/{p.TotalBytes:N0} bytes");
});

await splitter.SplitAsync(
    @"C:\Files\large-file.bin",
    @"C:\Packages\large-file",
    progress: progress);
```

`TransferProgress` contains:

| Property | Description |
|---|---|
| `ProcessedBytes` | Number of source/destination bytes processed |
| `TotalBytes` | Total file size |
| `CompletedChunks` | Number of chunks processed |
| `TotalChunks` | Total number of chunks |
| `Percent` | Completion percentage |

### Multi-file progress

For operations involving multiple files, use `MultiFileTransferProgress`:

```csharp
var progress = new Progress<MultiFileTransferProgress>(p =>
{
    Console.WriteLine(
        $"File {p.FileIndex + 1}/{p.FileCount}: " +
        $"{p.CurrentFileName} - " +
        $"{p.FileProgress.Percent:F1}%");
});

await splitter.SplitManyAsync(
    files,
    @"C:\Packages\my-package",
    progress: progress);
```

The same type can be used with `AssembleAllAsync`:

```csharp
await assembler.AssembleAllAsync(
    @"C:\Packages\my-package",
    @"C:\Restored",
    progress: progress);
```

## Cancellation

All asynchronous operations support cancellation:

```csharp
using var cts = new CancellationTokenSource();

var task = splitter.SplitAsync(
    @"C:\Files\large-file.bin",
    @"C:\Packages\large-file",
    cancellationToken: cts.Token);

// Cancel from another part of your application:
// cts.Cancel();

await task;
```

You can also cancel assembly:

```csharp
await assembler.AssembleAsync(
    @"C:\Packages\large-file",
    @"C:\Restored\large-file.bin",
    cancellationToken: cts.Token);
```

An `OperationCanceledException` is expected when cancellation occurs.

## WPF example

`IProgress<T>` works naturally with WPF. `Progress<T>` posts callbacks back to the synchronization context:

```csharp
private async Task SplitFileAsync()
{
    var progress = new Progress<TransferProgress>(p =>
    {
        ProgressBar.Value = p.Percent;
        StatusText.Text =
            $"{p.Percent:F1}% ({p.ProcessedBytes:N0} / {p.TotalBytes:N0})";
    });

    var options = new FileTransportOptions
    {
        ChunkSizeBytes = 64 * 1024 * 1024,
        BufferSizeBytes = 1024 * 1024,
        CompressChunks = false
    };

    var splitter = new FileSplitter();

    await splitter.SplitAsync(
        @"C:\Files\large-file.bin",
        @"C:\Packages\large-file",
        options,
        progress);
}
```

## WinForms example

The same API can be used from WinForms:

```csharp
private async Task AssembleFileAsync()
{
    var progress = new Progress<TransferProgress>(p =>
    {
        progressBar.Value = Math.Min(100, (int)p.Percent);
        statusLabel.Text = $"{p.Percent:F1}%";
    });

    var assembler = new FileAssembler();

    await assembler.AssembleAsync(
        @"C:\Packages\large-file",
        @"C:\Restored\large-file.bin",
        progress: progress);
}
```

## Recommended settings

For general-purpose file transport:

```csharp
var options = new FileTransportOptions
{
    ChunkSizeBytes = 64 * 1024 * 1024,
    BufferSizeBytes = 1024 * 1024,
    CompressChunks = false,
    HashDegreeOfParallelism = 2
};
```

For compressible data:

```csharp
var options = new FileTransportOptions
{
    ChunkSizeBytes = 64 * 1024 * 1024,
    BufferSizeBytes = 1024 * 1024,
    CompressChunks = true
};
```

`HashDegreeOfParallelism` is part of the transport options model and defaults to `2`.

## Package format

A package contains JSON metadata plus chunk files.

### `manifest.json`

The manifest stores information such as:

- Original filename
- Original file size
- Chunk size
- Compression state
- Overall SHA-256 hash
- Individual chunk metadata

Each chunk entry contains:

- Chunk index
- Original file offset
- Original chunk length
- SHA-256 hash
- Stored chunk length

### `index.json`

Multi-file packages contain an `index.json` that maps each original filename to its package entry.

Example structure:

```json
{
  "Version": 1,
  "Files": [
    {
      "OriginalFileName": "report.pdf",
      "RelativePath": "file_0000",
      "FileLength": 12345678,
      "OverallSha256": "..."
    }
  ]
}
```

## Integrity and safety

Every chunk is protected by a SHA-256 hash. During assembly, the library validates:

1. The chunk can be read.
2. The decompressed chunk has the expected length.
3. The chunk SHA-256 matches the manifest.
4. The final assembled file SHA-256 matches the manifest.

The assembled output is first written to a temporary file and only moved into the requested destination after successful validation.

The assembler also validates chunk paths from the manifest to prevent unsafe paths from escaping the package's `chunks` directory.

## Error handling

Typical exceptions include:

- `FileNotFoundException` — source, manifest, index, or chunk is missing.
- `ArgumentException` — invalid or missing input.
- `ArgumentOutOfRangeException` — invalid transport settings.
- `InvalidOperationException` — attempting to assemble a multi-file package without selecting a file.
- `InvalidDataException` — corrupted chunk, invalid manifest data, or final SHA-256 mismatch.
- `OperationCanceledException` — operation was cancelled.

Example:

```csharp
try
{
    await assembler.AssembleAsync(
        @"C:\Packages\large-file",
        @"C:\Restored\large-file.bin");
}
catch (InvalidDataException ex)
{
    Console.WriteLine($"Package integrity check failed: {ex.Message}");
}
catch (OperationCanceledException)
{
    Console.WriteLine("Operation cancelled.");
}
```

## API overview

### `FileSplitter`

| Method | Purpose |
|---|---|
| `SplitAsync(...)` | Split one file into a package |
| `SplitManyAsync(...)` | Split multiple files into one package |

### `FileAssembler`

| Method | Purpose |
|---|---|
| `ListPackageFilesAsync(...)` | List files contained in a package |
| `AssembleAsync(...)` | Assemble one selected file |
| `AssembleAllAsync(...)` | Assemble every file in a package |

### Models

- `FileTransportOptions`
- `FileManifest`
- `ChunkManifest`
- `PackageIndex`
- `PackageEntry`
- `TransferProgress`
- `MultiFileTransferProgress`
- `PackageFileInfo`

## Example workflow

A typical send/receive workflow looks like this:

```csharp
// Sender
var splitter = new FileSplitter();

await splitter.SplitAsync(
    @"C:\Files\video.mp4",
    @"C:\Transfer\video-package",
    new FileTransportOptions
    {
        ChunkSizeBytes = 128 * 1024 * 1024,
        CompressChunks = false
    });

// Transfer the package directory using your preferred transport.

// Receiver
var assembler = new FileAssembler();

await assembler.AssembleAsync(
    @"C:\Received\video-package",
    @"C:\Files\video.mp4");
```

The receiver does not need to manually calculate hashes or know whether compression was enabled.

## Requirements

- .NET Standard 2.0 compatible runtime/application.
- File-system access to the source and destination locations.
- Sufficient disk space for the package and assembled output.

## License

See [LICENSE](LICENSE).

## Repository

[https://git.saey.me/EonaCat/EonaCat.FileSplitter](https://git.saey.me/EonaCat/EonaCat.FileSplitter)
