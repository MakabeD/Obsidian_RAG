using System.IO.Compression;
using System.Text;
using configuration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using vaultReader;
using Xunit;

namespace ObsidianRAG.Tests.inputing;

public class VaultReaderTests
{
    [Fact]
    public async Task Two_zips_individually_under_the_cap_but_over_it_combined_are_rejected()
    {
        IOptions<RagOptions> options = Options.Create(new RagOptions
        {
            MaxZipTotalUncompressedBytes = 100
        });

        IFormFileCollection files = Files(
            ("a.zip", MakeZip(("a.md", new string('a', 60)))),
            ("b.zip", MakeZip(("b.md", new string('b', 60)))));

        await Assert.ThrowsAsync<UnsafeZipException>(
            () => VaultReader.reader(files, options));
    }

    [Fact]
    public async Task A_cancelled_token_stops_reading_before_any_work()
    {
        using CancellationTokenSource cts = new();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => VaultReader.reader(
                Files(("a.zip", MakeZip(("a.md", new string('a', 10))))),
                OptionsWithCap(100),
                cts.Token));
    }

    [Fact]
    public async Task A_zip_summing_exactly_to_the_cap_is_accepted()
    {
        List<DocumentData> documents = await VaultReader.reader(
            Files(("a.zip", MakeZip(("a.md", new string('a', 100))))),
            OptionsWithCap(100));

        DocumentData document = Assert.Single(documents);
        Assert.Equal("a.md", document.FileName);
    }

    [Fact]
    public async Task A_zip_one_byte_over_the_cap_is_rejected()
    {
        IOptions<RagOptions> options = Options.Create(new RagOptions
        {
            MaxZipTotalUncompressedBytes = 100
        });

        IFormFileCollection files = Files(
            ("a.zip", MakeZip(("a.md", new string('a', 101)))));

        await Assert.ThrowsAsync<UnsafeZipException>(
            () => VaultReader.reader(files, options));
    }

    [Fact]
    public async Task A_raw_md_file_counts_toward_the_request_total()
    {
        byte[] zip = MakeZip(("a.md", new string('a', 90)));

        byte[] md = Encoding.UTF8.GetBytes(new string('m', 20));

        IFormFileCollection files = Files(
            ("a.zip", zip),
            ("note.md", md));

        await Assert.ThrowsAsync<UnsafeZipException>(
            () => VaultReader.reader(files, OptionsWithCap(100)));
    }

    [Fact]
    public async Task The_cap_holds_regardless_of_file_order()
    {
        byte[] md = Encoding.UTF8.GetBytes(new string('m', 20));
        byte[] zip = MakeZip(("a.md", new string('a', 90)));

        IFormFileCollection files = Files(
            ("note.md", md),
            ("a.zip", zip));

        await Assert.ThrowsAsync<UnsafeZipException>(
            () => VaultReader.reader(files, OptionsWithCap(100)));
    }

    [Fact]
    public async Task The_error_names_the_request_scope_and_the_configured_limit()
    {
        UnsafeZipException exception = await Assert.ThrowsAsync<UnsafeZipException>(
            () => VaultReader.reader(
                Files(
                    ("a.zip", MakeZip(("a.md", new string('a', 60)))),
                    ("b.zip", MakeZip(("b.md", new string('b', 60))))),
                OptionsWithCap(100)));

        Assert.Contains("request", exception.Message);
        Assert.Contains("100", exception.Message);
    }

    [Fact]
    public async Task A_zip_entry_with_an_oversized_name_is_truncated_to_MaxFileNameLength()
    {
        string longName = new string('n', 300) + ".md";

        List<DocumentData> documents = await VaultReader.reader(
            Files(("a.zip", MakeZip((longName, new string('a', 60))))),
            OptionsWithCap(60));

        DocumentData document = Assert.Single(documents);
        Assert.Equal(new string('n', 260), document.FileName);
        Assert.Equal(new string('n', 260), document.Source);
        Assert.Equal(new string('a', 60), document.Content);
    }

    [Fact]
    public async Task A_bar_md_upload_with_an_oversized_name_is_truncated_to_MaxFileNameLength()
    {
        string longName = new string('m', 280) + ".md";

        List<DocumentData> documents = await VaultReader.reader(
            Files((longName, "hello"u8.ToArray())),
            OptionsWithCap(100));

        DocumentData document = Assert.Single(documents);
        Assert.Equal(new string('m', 260), document.FileName);
        Assert.Equal(new string('m', 260), document.Source);
        Assert.Equal("hello", document.Content);
    }

    [Fact]
    public async Task A_name_of_exactly_MaxFileNameLength_is_kept_whole_and_one_char_more_is_truncated()
    {
        string atCap = new string('x', 257) + ".md";
        string overCap = new string('x', 258) + ".md";

        IFormFileCollection files = Files((atCap, "ok"u8.ToArray()), (overCap, "ok"u8.ToArray()));

        List<DocumentData> documents = await VaultReader.reader(files, OptionsWithCap(100));

        Assert.Equal(2, documents.Count);
        Assert.Equal(atCap, documents[0].FileName);
        Assert.Equal(overCap[..260], documents[1].FileName);
    }

    private static IOptions<RagOptions> OptionsWithCap(long cap) =>
        Options.Create(new RagOptions { MaxZipTotalUncompressedBytes = cap });

    private static IFormFileCollection Files(params (string Name, byte[] Bytes)[] files)
    {
        FormFileCollection collection = new();
        foreach ((string name, byte[] bytes) in files)
        {
            MemoryStream stream = new(bytes);
            collection.Add(new FormFile(stream, 0, bytes.Length, "files", name));
        }

        return collection;
    }

    private static byte[] MakeZip(params (string Name, string Content)[] entries)
    {
        using MemoryStream stream = new();
        using (ZipArchive zip = new(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach ((string name, string content) in entries)
            {
                ZipArchiveEntry entry = zip.CreateEntry(name);
                using StreamWriter writer = new(entry.Open());
                writer.Write(content);
            }
        }

        return stream.ToArray();
    }
}
