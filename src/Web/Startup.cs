using ApplicationCore;
using Foundatio.Queues;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Web
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllers();

            services.AddJob<DoSomethingComplexJob, ItemToBeProcessed>(Configuration);

            // optional - switch to a foundatio queue here for alternative implementation

            // services.AddFoundatioQueueAdapter<ItemToBeProcessed, InMemoryQueue<ItemToBeProcessed>>();

            // OR services.AddFoundatioQueueAdapter<ItemToBeProcessed, RedisQueue<ItemToBeProcessed>>();
            // OR services.AddFoundatioQueueAdapter<ItemToBeProcessed, AzureServiceBusQueue<ItemToBeProcessed>>();
            // OR services.AddFoundatioQueueAdapter<ItemToBeProcessed, AzureStorageQueue<ItemToBeProcessed>>();
            // OR services.AddFoundatioQueueAdapter<ItemToBeProcessed, x => new AzureStorageQueue<ItemToBeProcessed>>(...);
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}