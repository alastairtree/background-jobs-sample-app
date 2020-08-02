using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ApplicationCore.BackgroundJobs;
using ApplicationCore.Storage;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Infrastructure
{
    public class FileStorage<TItem> : IStorage<TItem>
    {
        private readonly string filePath;
        private readonly ILogger<FileStorage<TItem>> logger;
        private readonly ISerializer<TItem[]> serializer;

        public FileStorage(ISerializer<TItem[]> serializer, IOptions<JobsOptions> options,
            ILogger<FileStorage<TItem>> logger)
        {
            this.serializer = serializer;
            this.logger = logger;
            var jobName = typeof(TItem).Name;

            filePath = Path.GetFullPath(string.Format(options.Value.StoragePath, jobName));
            logger.LogInformation("Using job resume file to {filePath}", filePath);
        }

        public async Task Save(TItem[] items)
        {
            var dirtyData = items.Any() || File.Exists(filePath);
            if (dirtyData)
            {
                var fileData = serializer.Serialize(items);

                Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                File.Delete(filePath);
                if (items.Any())
                {
                    using (var writer = File.CreateText(filePath))
                    {
                        await writer.WriteAsync(fileData);
                    }
                }
            }
        }

        public async Task<TItem[]> Get()
        {
            if (!Directory.Exists(Path.GetDirectoryName(filePath)) || !File.Exists(filePath))
                return Array.Empty<TItem>();

            string json;
            using (var sourceReader = File.OpenText(filePath))
            {
                json = await sourceReader.ReadToEndAsync();
            }

            var jobItems = json.Trim().Length > 0 ? serializer.Deserialize(json) : Array.Empty<TItem>();

            return jobItems;
        }
    }
}