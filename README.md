# Background jobs sample app for microservice

A sample app to demonstrate how to run background jobs inside an aspnet core microservice with graceful shutdown/resume and minimal resource utilisation.

## Quickstart

In one console window run the server:

```cmd
cd src\web
dotnet run
```

In another console (powershell) submit one or more "job" to the server

```powershell
$headers = New-Object "System.Collections.Generic.Dictionary[[String],[String]]"
$headers.Add("Content-Type", "application/json")
$headers.Add("Accept", "application/json")
$body = "{ `"id`": 123, `"message`": `"hello world`" }"
Invoke-RestMethod 'https://localhost:5001/queue' -Method 'POST' -Headers $headers -Body $body
```

Watch the console log output to understand what is happening inside the server.

Now add a number of items to the queue. Before they are done, stop the app using a CTRL-C in the first console window. Now restart the server and observer how it restarts the aborted queue item and resumes processing the queue. When complete the 'resume' json data file in AppData will be removed and the server will go back to sleep.

Features:

- Thread safe job queue to pass state from http request controllers to a background thread for later background processing
- Jobs will be processed one at a time from the queue in FIFO order
- Jobs start as soon as they arrive in queue - no polling
- Support for multiple job queues
- On a graceful shutdown currently executing job will be interrupted, and requeued for later
- On a graceful shutdown the current queue contents will be serialised to JSON and saved to disk
- On startup any previous unfinished queue items will be deserialised from disk and resume processing
- Periodically (every few seconds - configurable) the current job queue is saved to disk in case of unexpected shutdown
- Configuration loaded from appsettings, or any config source, using the options pattern
- Background jobs services are registered safely with the aspnet server host using IHostedService
- Implemented following the Clean Architecture style, targeting netstandard
- Efficient, low resource cost to run. No polling, timers or CPU utilisation on idle.
- Implmented as plain c# and zero dependencies, but...
- Comes with an adapter (See FoundatioFxAdapter) that allows you to swap out the queue implementation with one from Foundatio so can easily also support
    - Redis
    - Service Bus
    - Azure Storage
    - AWS SQS
    - See https://github.com/FoundatioFx/Foundatio

More info:

- https://docs.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services?view=aspnetcore-3.1&tabs=visual-studio
- https://docs.microsoft.com/en-us/dotnet/architecture/microservices/multi-container-microservice-net-applications/background-tasks-with-ihostedservice

## How to create a background job

Add a class and implement `IBackgroundJob<Something>` where `Something` is the type you would like to be passed from the queue

```csharp
public class YourJob : IBackgroundJob<int> // passing an int id to the job
{
    public Task ProcessItem(CancellationToken stoppingToken, int id) {
        Console.WriteLine($"Processing record {id}");
        return Task.CompletedTask;
    }
}
```

Register your new job in startup.cs

```csharp
services.AddJob<YourJob, int>(Configuration);

```

Get the current queue using dependency injection and add your item to the queue to be processed in the background

```csharp
IBackgroundJobQueue<ItemToBeProcessed> queue = (..... from constructor)
queue.QueueBackgroundWorkItem(123);
```

## Change to use an external queue such as Azure Service Bus or AWS SQS

Using the provided adapter you can use dependency injection to switch to any backing queue store supported by [FoundatioFx](https://github.com/FoundatioFx/Foundatio).

Register your new queue implementation after the job registration in startup.cs

```csharp
services.AddJob<YourJob, int>(Configuration);

// optional - switch to a foundatio queue here for service bus 
services.AddFoundatioQueueAdapter<ItemToBeProcessed, InMemoryQueue<ItemToBeProcessed>>();
// OR services.AddFoundatioQueueAdapter<ItemToBeProcessed, RedisQueue<ItemToBeProcessed>>();
// OR services.AddFoundatioQueueAdapter<ItemToBeProcessed, AzureServiceBusQueue<ItemToBeProcessed>>();
// OR services.AddFoundatioQueueAdapter<ItemToBeProcessed, AzureStorageQueue<ItemToBeProcessed>>();

```

## Limitations

* With many nodes each instance in a web farm will manages their own queue rather than distribute load when using the in memory queue. You may want to swap to a redis queue or similar.
* Saves in-memory queues using local file storage - you could re-implement IStorage for a cloud blobs/db or similar
* Ungraceful process termination may not have time to save the queue so some data loss possible. Refactor to use peek-lock & complete to fix if needed.
* Assumes queue items are fully serialisable to JSON for Save/Resume feature
* Background thread uses a single thread so queue length could explode with slow jobs and sufficient incoming requests

## Other background queues to consider

* https://www.hangfire.io/
* https://github.com/FoundatioFx/Foundatio#queues
* https://github.com/thangchung/awesome-dotnet-core#queue-and-messaging
