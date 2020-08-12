using System.Threading.Tasks;

namespace ApplicationCore.Storage
{
    public interface IStorage<TItem>
    {
        Task<TItem[]> Get();
        Task Save(TItem[] currentQueue);
    }
}