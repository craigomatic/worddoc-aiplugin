
using Azure.Storage.Blobs.Specialized;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

internal class WordDocWriter
{
    public static void AppendContentToLocalFile(string path, string content)
    {
        using (var stream = File.OpenWrite(path))
        {
            using (WordprocessingDocument wordprocessingDocument = WordprocessingDocument.Open(stream, true))
            {
                if (content is null)
                {
                    throw new ArgumentNullException(nameof(content));
                }

                MainDocumentPart? mainPart = wordprocessingDocument.MainDocumentPart;
                if (mainPart is null)
                {
                    throw new InvalidOperationException("The main document part is missing.");
                }

                Body? body = mainPart.Document.Body;
                if (body is null)
                {
                    throw new InvalidOperationException("The document body is missing.");
                }

                Paragraph para = body.AppendChild(new Paragraph());
                Run run = para.AppendChild(new Run());
                run.AppendChild(new Text(content));
            }
        }
    }

    public static async Task AppendContentToBlob(string blobUri, string content)
    {
        var blobClient = new BlockBlobClient(new Uri(blobUri));

        await _CreateBlobIfNotExists(blobClient);

        //NOTE: this is an inefficient implementation, we download the blob, open it, append, then upload it
        //couldn't get AppendBlob or BlockBlob to work nicely with OOXML SDK so this is the workaround for now
        
        var tempFile = Path.GetTempFileName();
        
        using (var stream = new FileStream(tempFile, FileMode.OpenOrCreate))
        {
            await blobClient.DownloadToAsync(stream);

            stream.Position = 0;

            using (WordprocessingDocument wordprocessingDocument = WordprocessingDocument.Open(stream, true))
            {
                if (content is null)
                {
                    throw new ArgumentNullException(nameof(content));
                }

                MainDocumentPart? mainPart = wordprocessingDocument.MainDocumentPart;
                if (mainPart is null)
                {
                    throw new InvalidOperationException("The main document part is missing.");
                }

                Body? body = mainPart.Document.Body;
                if (body is null)
                {
                    throw new InvalidOperationException("The document body is missing.");
                }

                Paragraph para = body.AppendChild(new Paragraph());
                Run run = para.AppendChild(new Run());
                run.AppendChild(new Text(content));

                wordprocessingDocument.Save();
            }

            stream.Position = 0;

            await blobClient.UploadAsync(stream);
        }            
    }

    private static async Task _CreateBlobIfNotExists(BlockBlobClient blobClient)
    {
        if (!await blobClient.ExistsAsync())
        {
            using (var ms = new MemoryStream())
            {
                var d = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document);
                
                var mainPart = d.AddMainDocumentPart();
                mainPart.Document = new Document();
                mainPart.Document.AppendChild(new Body());
                d.Save();
                
                ms.Position = 0;

                await blobClient.UploadAsync(ms);
            }
        }
    }
}
