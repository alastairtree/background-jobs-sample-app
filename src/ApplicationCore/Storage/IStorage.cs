using System.Threading.Tasks;

namespace ApplicationCore.Storage
{
    public interface IStorage<TItem>
    {
        Task Save(TItem[] currentQueue);
        Task<TItem[]> Get();
    }
}