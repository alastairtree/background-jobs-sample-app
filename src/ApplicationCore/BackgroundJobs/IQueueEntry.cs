using System;
using System.Threading.Tasks;

namespace ApplicationCore.BackgroundJobs
{
    public interface IQueueEntry<T>
    {
        DateTime DequeuedDate { get; }
        string Id { get; }
        T Value { get; }
        Task Complete();
    }
}