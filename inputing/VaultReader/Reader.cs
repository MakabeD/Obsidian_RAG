using System.IO.Compression;
using System.Text;
using configuration;
using Microsoft.Extensions.Options;
namespace vaultReader
{
    public sealed class UnsafeZipException : Exception
    {
        public UnsafeZipException(string message) : base(message) { }
        public UnsafeZipException(string message, Exception inner) : base(message, inner) { }
    }

    public class VaultReader()
    {
        private static readonly HashSet<string> AllowedExtensions =
            new(StringComparer.OrdinalIgnoreCase) { ".md", ".zip" };

        public static async Task<List<DocumentData>> reader(
            IFormFileCollection files,
            IOptions<RagOptions> options)
        {
            RagOptions opts = options.Value;
            List<DocumentData> processedFiles = new();

            foreach (IFormFile file in files)
            {
                string ext = Path.GetExtension(file.FileName);
                if (!AllowedExtensions.Contains(ext))
                {
                    throw new NotSupportedException(
                        $"Unsupported file type: '{ext}'. Only .md and .zip are accepted.");
                }

                if (file.Length > opts.MaxUploadBytes)
                {
                    throw new InvalidOperationException(
                        $"The file '{file.FileName}' exceeds the limit of {opts.MaxUploadBytes} bytes.");
                }

                if (ext.Equals(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    processedFiles.AddRange(await GetMdFromZipAsync(file, opts));
                }
                else
                {
                    processedFiles.Add(FileToString(file));
                }
            }

            return processedFiles;
        }

        private static DocumentData FileToString(IFormFile file)
        {
            using var stream = file.OpenReadStream();
            using var reader = new StreamReader(stream);

            return new DocumentData
            {
                Source = file.FileName,
                FileName = file.FileName,
                Content = reader.ReadToEnd()
            };
        }

        private static async Task<List<DocumentData>> GetMdFromZipAsync(IFormFile file, RagOptions opts)
        {
            List<DocumentData> processedZip = new();

            using var stream = file.OpenReadStream();
            using var zip = new ZipArchive(stream, ZipArchiveMode.Read);

            if (zip.Entries.Count > opts.MaxZipEntries)
            {
                throw new UnsafeZipException(
                    $"Zip contains {zip.Entries.Count} entries; the limit is {opts.MaxZipEntries}.");
            }

            long totalUncompressed = 0;

            foreach (ZipArchiveEntry entry in zip.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }

                if (entry.Length > opts.MaxZipEntryBytes)
                {
                    throw new InvalidOperationException(
                        $"Zip entry '{entry.FullName}' exceeds the limit of {opts.MaxZipEntryBytes} bytes (possible zip bomb).");
                }

                if (entry.CompressedLength > 0 && opts.MaxZipCompressionRatio > 0)
                {
                    long ratio = entry.Length / entry.CompressedLength;
                    if (ratio > opts.MaxZipCompressionRatio)
                    {
                        throw new UnsafeZipException(
                            $"Zip entry has a compression ratio of {ratio}x, exceeding the limit of {opts.MaxZipCompressionRatio}x.");
                    }
                }

                if (!entry.FullName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string safeSource = SanitizeEntryPath(entry.FullName);

                using Stream fileflow = entry.Open();
                var x=fileflow.Length;
                using var ms = new MemoryStream();
                await fileflow.CopyToAsync(ms);
                long entrySize = ms.Length;
                ms.Position = 0;

                totalUncompressed += entrySize;
                if (totalUncompressed > opts.MaxZipTotalUncompressedBytes)
                {
                    throw new UnsafeZipException(
                        $"Zip exceeds the total uncompressed limit of {opts.MaxZipTotalUncompressedBytes} bytes.");
                }

                using StreamReader reader = new(ms, leaveOpen: true);
                string content = await reader.ReadToEndAsync();

                processedZip.Add(new DocumentData
                {
                    Source = safeSource,
                    FileName = entry.Name,
                    Content = content
                });
            }

            return processedZip;
        }

        private static string SanitizeEntryPath(string fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                throw new UnsafeZipException("Zip entry has an empty path.");
            }

            if (fullName.IndexOfAny(new[] { ':', '*', '?', '<', '>', '|' }) >= 0)
            {
                throw new UnsafeZipException("Zip entry path contains forbidden characters.");
            }

            if (Path.IsPathRooted(fullName) || fullName.StartsWith('/') || fullName.StartsWith('\\'))
            {
                throw new UnsafeZipException("Zip entry path is rooted.");
            }

            string normalized = fullName.Replace('\\', '/');

            foreach (string segment in normalized.Split('/'))
            {
                if (segment == ".." || segment == ".")
                {
                    throw new UnsafeZipException("Zip entry path contains a traversal segment.");
                }
            }

            StringBuilder sb = new(normalized.Length);
            foreach (char ch in normalized)
            {
                bool ok =
                    (ch >= 'A' && ch <= 'Z') ||
                    (ch >= 'a' && ch <= 'z') ||
                    (ch >= '0' && ch <= '9') ||
                    ch == '.' || ch == '_' || ch == '-' || ch == '/' ||
                    ch == ' ' || ch == '\t' ||
                    char.IsLetter(ch);

                if (char.IsControl(ch) || !ok)
                {
                    throw new UnsafeZipException("Zip entry path contains forbidden characters.");
                }
                sb.Append(ch);
            }

            return sb.ToString();
        }
    }
}
