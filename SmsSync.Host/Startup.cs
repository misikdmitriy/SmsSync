using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using SmsSync.Background;
using SmsSync.Configuration;
using SmsSync.Services;
using SmsSync.Templates;

namespace SmsSync
{
    internal class Startup
    {
        private const string SendMessageRouteName = "sendMessage";
        
        private readonly ILogger _logger = Log.ForContext<Startup>();

        public Startup(IConfigurationRoot configuration)
        {
            Configuration = configuration;
        }

        private IConfigurationRoot Configuration { get; }

        public void ConfigureServices(IServiceCollection services)
        {
            _logger.Information("Configuring services...");

            services.AddMvc()
                .SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            var configuration = Configuration.GetSection("ServiceConfig").Get<AppConfiguration>();

            services.AddSingleton(configuration);
            services.AddSingleton(configuration.Background);
            services.AddSingleton(configuration.Http);
            services.AddSingleton(configuration.Routes);
            services.AddSingleton(configuration.Database);
            services.AddSingleton(configuration.Resources);
            
            services.AddTransient(sp => new SendSmsHandler(
                sp.GetRequiredService<IMessageBuilder>(),
                sp.GetRequiredService<IMessageHttpService>(),
                configuration.Routes.Single(r => r.Name.Equals(SendMessageRouteName)))
            );
            
            services.AddTransient<CommitSmsHandler>();
            services.AddTransient<FailSmsHandler>();

            services.AddTransient<IChainSmsHandler>(sp =>
            {
                var commitChain = new ChainSmsHandler(sp.GetRequiredService<CommitSmsHandler>(), null, null);
                var failChain = new ChainSmsHandler(sp.GetRequiredService<FailSmsHandler>(), null, null);
                var sendChain = new ChainSmsHandler(sp.GetRequiredService<SendSmsHandler>(), commitChain, failChain);

                return sendChain;
            });
            
            services.AddTransient<IInboxRepository, InboxRepository>();
            services.AddTransient<IJobsRepository, JobsRepository>();
            services.AddTransient<IResourceRepository, ResourceRepository>();
            services.AddTransient<IMessageHttpService, MessageHttpService>();

            services.AddSingleton<IHttpClientsPool, HttpClientsPool>();
            services.AddSingleton<IMessageBuilder, MessageBuilder>();

            services.AddTransient<PhoneNumberBuilder>();
            services.AddTransient<PlaceIdBuilder>();
            services.AddTransient<ServiceIdBuilder>();
            services.AddTransient<TicketIdBuilder>();
            services.AddTransient<ContentBuilder>();

            services.AddTransient<IDictionary<string, ITemplateBuilder>>(sp => new Dictionary<string, ITemplateBuilder>
            {
                [Constants.Templates.Content] = sp.GetRequiredService<ContentBuilder>(),
                [Constants.Templates.PhoneNumber] = sp.GetRequiredService<PhoneNumberBuilder>(),
                [Constants.Templates.ServiceId] = sp.GetRequiredService<ServiceIdBuilder>(),
                [Constants.Templates.TicketId] = sp.GetRequiredService<TicketIdBuilder>(),
                [Constants.Templates.PlaceId] = sp.GetRequiredService<PlaceIdBuilder>(),
            });

            services.AddTransient<BackgroundService>(sp => 
                new SyncHostedService(
                    sp.GetRequiredService<IChainSmsHandler>(),
                    sp.GetRequiredService<IInboxRepository>(),
                    configuration.Background,
                    configuration.Database.MaxBatchSize
                )
            );

            _logger.Information("Services configured.");
        }
    }
}