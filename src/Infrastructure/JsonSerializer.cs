using System.Text.Json;
using ApplicationCore.Storage;

namespace Infrastructure
{
    public class JsonSerializer<TJobItem> : ISerializer<TJobItem>
    {
        public TJobItem Deserialize(string line) => JsonSerializer.Deserialize<TJobItem>(line);
        public string Serialize(TJobItem item) => JsonSerializer.Serialize(item);
    }
}