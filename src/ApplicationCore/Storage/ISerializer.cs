namespace ApplicationCore.Storage
{
    public interface ISerializer<TJobItem>
    {
        string Serialize(TJobItem item);
        TJobItem Deserialize(string line);
    }
}