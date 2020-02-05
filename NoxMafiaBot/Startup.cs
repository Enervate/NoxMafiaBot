using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace NoxMafiaBot
{

    public class Startup
    {
        public IConfigurationRoot Configuration { get; }
        public static List<Mafia.Game> Games;
        public static List<Mafia.PlayerRole> DefaultRoles;

        public Startup(string[] args)
        {
            var builder = new ConfigurationBuilder()        // Create a new instance of the config builder
                .SetBasePath(AppContext.BaseDirectory);     // Specify the default location for the config file

            Configuration = builder.Build();                // Build the configuration

            Games = new List<Mafia.Game>();                 // Instantiate our internal games list
            DefaultRoles = new List<Mafia.PlayerRole>();    // Instantiate our default role list

            // Load the list of default roles at runtime
            try
            {
                string json = File.ReadAllText("DefaultRoles.json");
                var defaultRoles = JsonConvert.DeserializeObject<Mafia.DefaultRoleCollection>(json);

                Array Powers = Enum.GetValues(typeof(Mafia.PowerFlags));
                Array Alignments = Enum.GetValues(typeof(Mafia.PlayerAlignment));

                foreach (Mafia.DefaultRole role in defaultRoles.Roles)
                {
                    Mafia.PowerFlags drPowers = Mafia.PowerFlags.None;
                    Mafia.PlayerAlignment drAlignment = Mafia.PlayerAlignment.Town; // Set alignment to town by default in case the specified alignment is invalid

                    foreach (string thisPower in role.Powers) // Set power flags for each power specified in the JSON file
                    {
                        foreach (Mafia.PowerFlags val in Powers)
                        {
                            string powerFlag = Enum.GetName(typeof(Mafia.PowerFlags), val);

                            if (powerFlag == thisPower)
                                drPowers |= val;
                        }
                    }

                    foreach (Mafia.PlayerAlignment val in Alignments) // Set alignment as specified in the JSON file
                    {
                        if (Enum.GetName(typeof(Mafia.PlayerAlignment), val) == role.Alignment)
                            drAlignment = val;
                    }

                    DefaultRoles.Add(new Mafia.PlayerRole(role.Name, role.Description, drPowers, role.Charges, drAlignment));
                }
            }
            catch (Exception err)
            {
                System.Diagnostics.Debug.WriteLine(err.Message);
            }
        }

        public static async Task RunAsync(string[] args)
        {
            var startup = new Startup(args);
            await startup.RunAsync();
        }

        public async Task RunAsync()
        {
            var services = new ServiceCollection();             // Create a new instance of a service collection
            ConfigureServices(services);

            var provider = services.BuildServiceProvider();     // Build the service provider
            provider.GetRequiredService<LoggingService>();      // Start the logging service
            provider.GetRequiredService<CommandHandler>(); 		// Start the command handler service

            await provider.GetRequiredService<StartupService>().StartAsync();       // Start the startup service
            await Task.Delay(-1);                               // Keep the program alive
        }

        private void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton(new DiscordSocketClient(new DiscordSocketConfig
            {                                       // Add discord to the collection
                LogLevel = LogSeverity.Verbose,     // Tell the logger to give Verbose amount of info
                MessageCacheSize = 1000             // Cache 1,000 messages per channel
            }))
            .AddSingleton(new CommandService(new CommandServiceConfig
            {                                       // Add the command service to the collection
                LogLevel = LogSeverity.Verbose,     // Tell the logger to give Verbose amount of info
                DefaultRunMode = RunMode.Async,     // Force all commands to run async by default
            }))
            .AddSingleton<CommandHandler>()         // Add the command handler to the collection
            .AddSingleton<StartupService>()         // Add startupservice to the collection
            .AddSingleton<LoggingService>()         // Add loggingservice to the collection
            .AddSingleton<Random>()                 // Add random to the collection
            .AddSingleton(Configuration);           // Add the configuration to the collection
        }
    }
}
