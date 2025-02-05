using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using CommandLine;

using Microsoft.Extensions.Configuration;

using Newtonsoft.Json;

using Plugfy.Core.Commons.Runtime;

namespace Plugfy.Core.Extension.Library.Runner.DotNet
{
    // VERB: LIST
    [Verb("list", HelpText = "List available assemblies.")]
    public class ListOptions
    {
        [Option('t', "communicationType", Required = false, HelpText = "Type of communication to use (e.g., STDInOut, NamedPipes).")]
        public string CommunicationType { get; set; }  = "STDInOut";

        [Option('i', "interactive", Required = false, HelpText = "Enable interactive mode.")]
        public bool Interactive { get; set; }

        [Option('c', "caller", Required = false, HelpText = "Parameters for the Caller Communications Extensions (JSON format).")]
        public string CallerExtensionsParameters { get; set; }

        [Option('l', "log", Required = false, HelpText = "Parameters for the Log Extensions (JSON format).")]
        public string LogExtensionsParameters { get; set; }
    }

    // VERB: INFO
    [Verb("info", HelpText = "Get detailed information of an assembly.")]
    public class InfoOptions
    {
        [Option('t', "communicationType", Required = false, HelpText = "Type of communication to use (e.g., STDInOut, NamedPipes).")]
        public string CommunicationType { get; set; } = "STDInOut";

        // JSON content with { "AssemblyName": "xxx" }
        [Value(0, MetaName = "commandContent", Required = true, HelpText = "JSON content with { \"AssemblyName\": \"xxx\" }")]
        public string Content { get; set; }

        [Option('i', "interactive", Required = false, HelpText = "Enable interactive mode.")]
        public bool Interactive { get; set; }

        [Option('c', "caller", Required = false, HelpText = "Parameters for the Caller Communications Extensions (JSON format).")]
        public string CallerExtensionsParameters { get; set; }

        [Option('l', "log", Required = false, HelpText = "Parameters for the Log Extensions (JSON format).")]
        public string LogExtensionsParameters { get; set; }
    }

    // VERB: RUN
    [Verb("run", HelpText = "Execute a method in an assembly.")]
    public class RunOptions
    {
        [Option('t', "communicationType", Required = false, HelpText = "Type of communication to use (e.g., STDInOut, NamedPipes).")]
        public string CommunicationType { get; set; } = "STDInOut";

        // JSON content with { "AssemblyName": "xxx", "Class": "xxx", "Method": "xxx", ... }
        [Value(0, MetaName = "commandContent", Required = true, HelpText = "JSON content with { \"AssemblyName\": \"xxx\", \"Class\": \"xxx\", \"Method\": \"xxx\", … }")]
        public string Content { get; set; }

        [Option('i', "interactive", Required = false, HelpText = "Enable interactive mode.")]
        public bool Interactive { get; set; }

        [Option('c', "caller", Required = false, HelpText = "Parameters for the Caller Communications Extensions (JSON format).")]
        public string CallerExtensionsParameters { get; set; }

        [Option('l', "log", Required = false, HelpText = "Parameters for the Log Extensions (JSON format).")]
        public string LogExtensionsParameters { get; set; }
    }

    // Class for encapsulating dynamic commands.
    public class DynamicCommand
    {
        public string Type { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
    }

    class Program
    {
        static async Task<int> Main(string[] args)
        {
            // CommandLineParser maps the verb to the corresponding options class.
            return await Parser.Default.ParseArguments<ListOptions, InfoOptions, RunOptions>(args)
                .MapResult(
                    async (ListOptions opts) => await ExecuteListCommand(opts),
                    async (InfoOptions opts) => await ExecuteInfoCommand(opts),
                    async (RunOptions opts) => await ExecuteRunCommand(opts),
                    errs => Task.FromResult(1)
                );
        }

        // Process the 'list' command.
        private static async Task<int> ExecuteListCommand(ListOptions options)
        {
            try
            {
                IDataCommunication communication = InitializeCommunication(options.CommunicationType);
                // Initialize communication with optional caller parameters.
                await communication.InitializeAsync(options.CallerExtensionsParameters);


                if (options.Interactive)
                {
                    // In interactive mode, listen for incoming commands.
                    communication.DataReceived += async (sender, data) =>
                    {
                        Console.WriteLine($"[Runner -> Library] Received: {data.Data}");
                        var cmd = JsonConvert.DeserializeObject<DynamicCommand>(data.Data.ToString());
                        await ProcessCommandAsync(cmd, communication);
                    };

                    await communication.SendDataAsync("Interactive mode started. Awaiting commands...");
                    while (!communication.IsClosed)
                    {
                        await Task.Delay(500);
                    }
                    await communication.SendDataAsync("Interactive mode ended.");
                }
                else
                {
                    // Create a dynamic command for 'list'
                    var command = new DynamicCommand
                    {
                        Type = "list",
                        Parameters = new Dictionary<string, object>()
                    };
                    await ProcessCommandAsync(command, communication);
                    await communication.CloseAsync();
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex}");
                return 1;
            }
        }

        // Process the 'info' command.
        private static async Task<int> ExecuteInfoCommand(InfoOptions options)
        {
            try
            {
                IDataCommunication communication = InitializeCommunication(options.CommunicationType);
                await communication.InitializeAsync(options.CallerExtensionsParameters);

                if (options.Interactive)
                {
                    communication.DataReceived += async (sender, data) =>
                    {
                        Console.WriteLine($"[Runner -> Library] Received: {data.Data}");
                        var cmd = JsonConvert.DeserializeObject<DynamicCommand>(data.Data.ToString());
                        await ProcessCommandAsync(cmd, communication);
                    };
                    await communication.SendDataAsync("Interactive mode started. Awaiting commands...");
                }

                var parameters = JsonConvert.DeserializeObject<Dictionary<string, object>>(options.Content);
                var command = new DynamicCommand
                {
                    Type = "info",
                    Parameters = parameters
                };

                await ProcessCommandAsync(command, communication);
                if (!options.Interactive)
                {
                    await communication.CloseAsync();
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex}");
                return 1;
            }
        }

        // Process the 'run' command.
        private static async Task<int> ExecuteRunCommand(RunOptions options)
        {
            try
            {
                IDataCommunication communication = InitializeCommunication(options.CommunicationType);
                await communication.InitializeAsync(options.CallerExtensionsParameters);

                if (options.Interactive)
                {
                    communication.DataReceived += async (sender, data) =>
                    {
                        Console.WriteLine($"[Runner -> Library] Received: {data.Data}");
                        var cmd = JsonConvert.DeserializeObject<DynamicCommand>(data.Data.ToString());
                        await ProcessCommandAsync(cmd, communication);
                    };
                    await communication.SendDataAsync("Interactive mode started. Awaiting commands...");
                }

                var parameters = JsonConvert.DeserializeObject<Dictionary<string, object>>(options.Content);
                var command = new DynamicCommand
                {
                    Type = "run",
                    Parameters = parameters
                };

                await ProcessCommandAsync(command, communication);
                if (!options.Interactive)
                {
                    await communication.CloseAsync();
                }
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex}");
                return 1;
            }
        }

        // Process dynamic commands.
        private static async Task ProcessCommandAsync(DynamicCommand command, IDataCommunication communication)
        {
            try
            {
                var runtime = new Runtime(GetConfigPath());

                switch (command.Type.ToLowerInvariant())
                {
                    case "list":
                        var assemblies = runtime.ListAssemblies(communication);
                        await communication.SendDataAsync(assemblies);
                        break;

                    case "info":
                        // Expect the JSON to contain "AssemblyName" (try case-insensitively)
                        string assemblyName = null;
                        if (command.Parameters.TryGetValue("AssemblyName", out object val))
                        {
                            assemblyName = val.ToString();
                        }
                        else
                        {
                            // Attempt case-insensitive search.
                            var pair = command.Parameters.FirstOrDefault(kvp => kvp.Key.Equals("assemblyname", StringComparison.OrdinalIgnoreCase));
                            if (!string.IsNullOrEmpty(pair.Key))
                                assemblyName = pair.Value.ToString();
                        }
                        if (string.IsNullOrEmpty(assemblyName))
                        {
                            await communication.SendDataAsync("Error: 'AssemblyName' parameter is missing.");
                            break;
                        }
                        var description = runtime.DescribeAssembly(assemblyName);
                        await communication.SendDataAsync(description);
                        break;

                    case "run":
                        // For 'run', expect properties like "AssemblyName", "Class", "Method", etc.
                        var methodCallJson = JsonConvert.SerializeObject(command.Parameters);
                        var methodCall = JsonConvert.DeserializeObject<Runtime.MethodCall>(methodCallJson);
                        var result = runtime.ExecuteMethod(methodCall, communication);

                        if( result != null)
                            await communication.SendDataAsync(result);

                        break;
                    case "exit":
                        await communication.SendDataAsync("Runner received exit command. Shutting down...");
                        await communication.CloseAsync();
                        break;

                    default:
                        await communication.SendDataAsync($"Unknown command: {command.Type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                await communication.SendDataAsync(ex);
            }
        }

        // Initialize the communication extension.
        private static IDataCommunication InitializeCommunication(string communicationType)
        {
            // Load configuration from appsettings.json.
            IConfiguration config = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Get the configured path for the desired communication extension.
            string extensionsPath = config[$"Extensions:Communications:{communicationType}:LibrariesPath"];
            if (string.IsNullOrEmpty(extensionsPath))
            {
                throw new DirectoryNotFoundException($"Configuration missing for communication type '{communicationType}'. Check appsettings.json.");
            }

            extensionsPath = Path.GetFullPath(extensionsPath);
            if (!Directory.Exists(extensionsPath))
            {
                throw new DirectoryNotFoundException($"Communication extensions directory '{extensionsPath}' not found.");
            }

            var dllFiles = Directory.GetFiles(extensionsPath, "*.dll");
            foreach (var dll in dllFiles)
            {
                try
                {
                    var assembly = Assembly.LoadFrom(dll);
                    var types = assembly.GetTypes();
                    foreach (var type in types)
                    {
                        if (typeof(IDataCommunication).IsAssignableFrom(type) && !type.IsAbstract && !type.IsInterface)
                        {
                            var instance = (IDataCommunication)Activator.CreateInstance(type);
                            if (instance.Name.Equals(communicationType, StringComparison.OrdinalIgnoreCase))
                            {
                                return instance;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load communication extension from '{dll}': {ex}");
                }
            }
            throw new ArgumentException($"Unsupported communication type: {communicationType}");
        }

        private static string GetConfigPath()
        {
            return "./appsettings.json";
        }
    }
}
