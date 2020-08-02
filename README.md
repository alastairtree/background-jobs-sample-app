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

## Limitations

* With many nodes each instance in a web farm will manages their own queue rather than distribute load.
* Uses local file storage - you could re-implement IStorage for a database if desired
* Ungraceful process termination may not have time to save the queue so some data loss possible.
* Assumes queue items are serialisable
* Background thread uses a single thread so queue length could explode with slow jobs and sufficient incoming requests

## Other background queues to consider

* https://www.hangfire.io/
* https://github.com/FoundatioFx/Foundatio#queues
* https://github.com/thangchung/awesome-dotnet-core#queue-and-messaging
