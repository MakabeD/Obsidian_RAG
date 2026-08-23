using System.IO.Compression;
namespace vaultReader
{
    public class VaultReader()
    {
        public static async Task<List<DocumentData>> reader(IFormFileCollection files)
        {
            List<DocumentData> ProcesedFiles=new List<DocumentData>();
            
            foreach (var file in files)
            {
                if( file.FileName.EndsWith(".zip"))
                {
                    Task<List<DocumentData>> task_=getmdfromzip(file);
                    List<DocumentData> ProcessedZip =  await task_;
                    ProcesedFiles.AddRange(ProcessedZip); 
                }
                else if(file.FileName.EndsWith(".md"))
                {
                    ProcesedFiles.Add(filetostring(file));
                }

            }
            return ProcesedFiles;
        }

        public static DocumentData filetostring(IFormFile file)
        { 
            var stream = file.OpenReadStream();
            var reader = new StreamReader(stream);

            return new DocumentData
            {
                Source=file.FileName,
                FileName=file.FileName,
                Content=reader.ReadToEnd()
            };
            

        }
        public static async Task<List<DocumentData>> getmdfromzip(IFormFile file)
        {
            List<DocumentData> ProcessedZip = new List<DocumentData>();

            using (var stream = file.OpenReadStream())
            using (var zip = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (ZipArchiveEntry entry in zip.Entries)
                {

                    if (entry.FullName.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
                    {
                        string source = entry.FullName;
                        string filename = entry.Name; 

                        
                        string content = string.Empty;
                        
                        using (Stream fileflow = entry.Open())
                        using (StreamReader reader = new StreamReader(fileflow))
                        {
                            
                            content = await reader.ReadToEndAsync();
                        }

                        ProcessedZip.Add(new DocumentData 
                        {
                            Source = source,
                            FileName = filename,
                            Content = content
                        });

                       
                    }
                }
            }
            return ProcessedZip;
        }

    }


}