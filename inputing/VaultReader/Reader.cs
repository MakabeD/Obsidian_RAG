using System.IO.Compression;
using configuration;
using Microsoft.Extensions.Options;
namespace vaultReader
{
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

                if (!entry.FullName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using Stream fileflow = entry.Open();
                using StreamReader reader = new(fileflow);
                string content = await reader.ReadToEndAsync();

                processedZip.Add(new DocumentData
                {
                    Source = entry.FullName,
                    FileName = entry.Name,
                    Content = content
                });
            }

            return processedZip;
        }
    }
}
