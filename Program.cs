using System.Diagnostics;

namespace ConvertCli;

internal static class Program
{
    private static readonly HashSet<string> SupportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "png", "jpg", "jpeg", "webp", "bmp", "gif", "tif", "tiff", "ico"
    };

    private static int Main(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            PrintUsage();
            return args.Length == 0 ? 2 : 0;
        }

        if (args[0] is "--version" or "-v")
        {
            Console.WriteLine("convert 0.1.0");
            return 0;
        }

        try
        {
            if (args.Length == 2)
                return ConvertFile(args[0], NormalizeFormat(args[1]));
            if (args.Length == 3)
                return ConvertDirectory(args[0], NormalizeFormat(args[1]), NormalizeFormat(args[2]));
            PrintUsage();
            return 2;
        }
        catch (CliException ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 1;
        }
    }

    private static int ConvertFile(string input, string outputFormat)
    {
        var source = Path.GetFullPath(input);
        if (!File.Exists(source)) throw new CliException($"No existe el archivo: {input}");
        var output = Path.ChangeExtension(source, outputFormat);
        if (SamePath(source, output)) throw new CliException("El formato de salida coincide con el archivo de entrada.");
        if (File.Exists(output))
        {
            Console.Error.WriteLine($"Omitido: ya existe {output}");
            return 1;
        }
        ConvertOne(source, output, outputFormat);
        Console.WriteLine($"{source} → {output}");
        return 0;
    }

    private static int ConvertDirectory(string directory, string inputFormat, string outputFormat)
    {
        var folder = Path.GetFullPath(directory);
        if (!Directory.Exists(folder)) throw new CliException($"No existe la carpeta: {directory}");
        if (inputFormat == outputFormat) throw new CliException("El formato de entrada y salida deben ser distintos.");

        var files = Directory.EnumerateFiles(folder)
            .Where(path => string.Equals(NormalizeExistingExtension(path), inputFormat, StringComparison.OrdinalIgnoreCase))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (files.Length == 0)
        {
            Console.WriteLine($"No se encontraron archivos .{inputFormat} en {folder}");
            return 0;
        }

        var failures = 0;
        foreach (var source in files)
        {
            var output = Path.ChangeExtension(source, outputFormat);
            if (File.Exists(output))
            {
                Console.Error.WriteLine($"Omitido: ya existe {output}");
                failures++;
                continue;
            }
            try
            {
                ConvertOne(source, output, outputFormat);
                Console.WriteLine($"{Path.GetFileName(source)} → {Path.GetFileName(output)}");
            }
            catch (CliException ex)
            {
                Console.Error.WriteLine($"Error en {Path.GetFileName(source)}: {ex.Message}");
                failures++;
            }
        }
        return failures == 0 ? 0 : 1;
    }

    private static void ConvertOne(string source, string destination, string format)
    {
        var ffmpeg = FindFfmpeg();
        var tempFiles = new List<string>();
        var destinationDirectory = Path.GetDirectoryName(destination)!;
        var extension = Path.GetExtension(destination);
        var temporaryOutput = format == "ico"
            ? Path.Combine(destinationDirectory, $".{Path.GetFileNameWithoutExtension(destination)}.{Guid.NewGuid():N}.tmp.ico")
            : Path.Combine(destinationDirectory, $".{Path.GetFileNameWithoutExtension(destination)}.{Guid.NewGuid():N}.tmp{extension}");
        try
        {
            if (format == "ico")
            {
                var sizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
                var pngs = new List<byte[]>();
                foreach (var size in sizes)
                {
                    var temp = Path.Combine(Path.GetTempPath(), $"convert-{Guid.NewGuid():N}.png");
                    tempFiles.Add(temp);
                    RunFfmpeg(ffmpeg, source, temp, $"scale={size}:{size}:force_original_aspect_ratio=decrease,pad={size}:{size}:(ow-iw)/2:(oh-ih)/2:color=0x00000000,format=rgba");
                    pngs.Add(File.ReadAllBytes(temp));
                }
                WriteIco(temporaryOutput, sizes, pngs);
            }
            else
            {
                RunFfmpeg(ffmpeg, source, temporaryOutput, null);
            }

            if (!File.Exists(temporaryOutput) || new FileInfo(temporaryOutput).Length == 0)
                throw new CliException("FFmpeg no produjo un archivo de salida.");
            File.Move(temporaryOutput, destination);
        }
        catch (IOException ex)
        {
            throw new CliException($"No se pudo escribir la salida: {ex.Message}");
        }
        finally
        {
            TryDelete(temporaryOutput);
            foreach (var path in tempFiles) TryDelete(path);
        }
    }

    private static void RunFfmpeg(string executable, string source, string output, string? filter)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, RedirectStandardError = true, CreateNoWindow = true };
        start.ArgumentList.Add("-hide_banner");
        start.ArgumentList.Add("-loglevel"); start.ArgumentList.Add("error");
        start.ArgumentList.Add("-nostdin"); start.ArgumentList.Add("-i"); start.ArgumentList.Add(source);
        if (filter is not null) { start.ArgumentList.Add("-vf"); start.ArgumentList.Add(filter); }
        start.ArgumentList.Add("-frames:v"); start.ArgumentList.Add("1");
        start.ArgumentList.Add("-y"); start.ArgumentList.Add(output);
        using var process = Process.Start(start) ?? throw new CliException("No se pudo iniciar FFmpeg.");
        var errorTask = process.StandardError.ReadToEndAsync();
        process.WaitForExit();
        var error = errorTask.GetAwaiter().GetResult().Trim();
        if (process.ExitCode != 0)
            throw new CliException(string.IsNullOrWhiteSpace(error) ? $"FFmpeg terminó con código {process.ExitCode}." : error);
    }

    private static void WriteIco(string path, IReadOnlyList<int> sizes, IReadOnlyList<byte[]> images)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Count);
        var offset = 6 + (16 * sizes.Count);
        for (var i = 0; i < sizes.Count; i++)
        {
            var size = sizes[i];
            writer.Write((byte)(size == 256 ? 0 : size)); writer.Write((byte)(size == 256 ? 0 : size));
            writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
            writer.Write(images[i].Length); writer.Write(offset); offset += images[i].Length;
        }
        foreach (var image in images) writer.Write(image);
    }

    private static string FindFfmpeg()
    {
        var configured = Environment.GetEnvironmentVariable("FFMPEG_PATH");
        var candidate = string.IsNullOrWhiteSpace(configured) ? "ffmpeg" : configured;
        try
        {
            var start = new ProcessStartInfo(candidate) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
            start.ArgumentList.Add("-version");
            using var process = Process.Start(start);
            if (process is null) throw new InvalidOperationException();
            process.WaitForExit(3000);
            if (!process.HasExited) { process.Kill(true); throw new InvalidOperationException(); }
            if (process.ExitCode == 0) return candidate;
        }
        catch { }
        throw new CliException("No se encontró FFmpeg. Instálalo y agrégalo al PATH, o define FFMPEG_PATH con la ruta a ffmpeg.exe.");
    }

    private static string NormalizeFormat(string value)
    {
        var format = value.Trim().TrimStart('.').ToLowerInvariant();
        if (!SupportedFormats.Contains(format))
            throw new CliException($"Formato no soportado: {value}. Usa: {string.Join(", ", SupportedFormats.Order())}.");
        return format == "jpeg" ? "jpg" : format == "tiff" ? "tif" : format;
    }

    private static string NormalizeExistingExtension(string path)
    {
        var extension = Path.GetExtension(path).TrimStart('.').ToLowerInvariant();
        return extension == "jpeg" ? "jpg" : extension == "tiff" ? "tif" : extension;
    }

    private static bool SamePath(string a, string b) => string.Equals(Path.GetFullPath(a), Path.GetFullPath(b), StringComparison.OrdinalIgnoreCase);
    private static void TryDelete(string path) { try { if (File.Exists(path)) File.Delete(path); } catch { } }
    private static void PrintUsage()
    {
        Console.WriteLine("convert <archivo> <formato_salida>");
        Console.WriteLine("convert <carpeta> <formato_origen> <formato_salida>");
        Console.WriteLine("Ejemplos: convert foto.png webp | convert .\\images png jpg");
        Console.WriteLine("Opciones: --help, --version. No sobrescribe salidas existentes.");
    }

    private sealed class CliException(string message) : Exception(message);
}
