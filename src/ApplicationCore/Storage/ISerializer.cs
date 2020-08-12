namespace ApplicationCore.Storage
{
    public interface ISerializer<TJobItem>
    {
        TJobItem Deserialize(string line);
        string Serialize(TJobItem item);
    }
}