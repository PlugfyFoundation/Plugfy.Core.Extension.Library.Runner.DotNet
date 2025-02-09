using System.Linq.Expressions;
using System.Reflection;

using Microsoft.Extensions.Configuration;

using Plugfy.Core.Commons.Communication;

namespace Plugfy.Core.Extension.Library.Runner.DotNet;
public class Runtime
{
    public string ConfigPath { get; }
    public string AssembliesPath { get; private set; }

    public event EventHandler<RuntimeEventArgs> OnEvent;

    public Runtime(string configPath)
    {
        ConfigPath = configPath;
        AssembliesPath = LoadConfig(configPath);
    }

    public void RaiseEvent(IDataCommunication communication, string source, string eventName, string message, dynamic data = null)
    {
        OnEvent?.Invoke(this, new RuntimeEventArgs
        {
            Source = source,
            EventName = eventName,
            Message = message,
            Data = data
        });

        communication.SendDataAsync(new { source, eventName, message, data }).Wait();

    }

    public List<string> ListAssemblies(IDataCommunication communication)
    {
        if (!Directory.Exists(AssembliesPath))
            throw new DirectoryNotFoundException($"Assemblies directory '{Path.GetFullPath(AssembliesPath)}' does not exist.");

        return Directory.GetFiles(AssembliesPath, "*.dll").Select(Path.GetFileName).ToList();
    }

    public AssemblyDescription DescribeAssembly(string assemblyName)
    {
        string assemblyPath = Path.Combine(AssembliesPath, assemblyName);
        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Assembly '{assemblyName}' not found.");

        var assembly = Assembly.LoadFrom(assemblyPath);

        var description = new AssemblyDescription
        {
            FullName = assembly.FullName,
            Types = assembly.GetTypes().Select(t => new TypeDescription
            {
                Name = t.FullName,
                Methods = t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Select(m => new MethodDescription
                {
                    Name = m.Name,
                    ReturnType = m.ReturnType.FullName,
                    Parameters = m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}").ToList()
                }).ToList(),
                Events = t.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Select(e => new EventDescription
                {
                    Name = e.Name,
                    EventHandlerType = e.EventHandlerType.FullName
                }).ToList(),
                Fields = t.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static).Select(f => new FieldDescription
                {
                    Name = f.Name,
                    FieldType = f.FieldType.FullName
                }).ToList()
            }).ToList()
        };

        return description;
    }

    public dynamic ExecuteMethod(MethodCall methodCall, IDataCommunication communication)
    {
        string assemblyPath = Path.Combine(AssembliesPath, methodCall.Assembly);
        if (!File.Exists(assemblyPath))
            throw new FileNotFoundException($"Assembly '{methodCall.Assembly}' not found.");

        try
        {
            var assembly = Assembly.LoadFrom(assemblyPath);
            var type = assembly.GetType(methodCall.Class) ?? throw new InvalidOperationException($"Class '{methodCall.Class}' not found in assembly '{methodCall.Assembly}'.");

            ConstructorInfo constructor = null;
            object instance = null;

            if (methodCall.ConstructorParameters != null)
            {
                var constructors = type.GetConstructors();
                foreach (var ctor in constructors)
                {
                    var ctorParams = ctor.GetParameters();
                    if (ctorParams.Length == methodCall.ConstructorParameters.Length)
                    {
                        AdjustParameterTypes(methodCall.ConstructorParameters, ctorParams);
                        constructor = ctor;
                        break;
                    }
                }

                if (constructor == null)
                    throw new InvalidOperationException($"Suitable constructor not found for class '{methodCall.Class}'.");

                instance = constructor.Invoke(methodCall.ConstructorParameters);
            }
            else
            {
                instance = Activator.CreateInstance(type);
            }

            SubscribeToEvents(communication, type, instance);

            var method = type.GetMethod(methodCall.Method) ?? throw new InvalidOperationException($"Method '{methodCall.Method}' not found in class '{methodCall.Class}'.");
            AdjustParameterTypes(methodCall.Parameters, method.GetParameters());

            try {
                RaiseEvent(communication, $"{methodCall.Class}.{methodCall.Method}", "execute", $"Executing method '{methodCall.Method}'...", methodCall);

                var result = method.Invoke(instance, methodCall.Parameters);
                RaiseEvent(communication, $"{methodCall.Class}.{methodCall.Method}", "finality", $"Method '{methodCall.Method}' executed successfully.", result);
                return result;
            }
            catch (Exception ex) {
                RaiseEvent(communication, $"{methodCall.Class}.{methodCall.Method}", "error", ex.InnerException?.Message ?? ex.Message, ex.InnerException ?? ex);
                RaiseEvent(communication, $"{methodCall.Class}.{methodCall.Method}", "finality", $"Method '{methodCall.Method}' failed.", null);
                return null;
            }


        }
        catch (Exception ex){
            RaiseEvent(communication, $"Plugfy.Runner", "error", ex.InnerException?.Message ?? ex.Message, ex.InnerException ?? ex);
            RaiseEvent(communication, $"Plugfy.Runner", "finality", $"Method '{methodCall.Method}' failed.", null);
            return null;
        }
         
    }

    private void SubscribeToEvents(IDataCommunication communication, Type type, object instance)
    {
        var events = type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static);

        foreach (var ev in events)
        {

            try
            {
                var eventHandler = CreateEventHandlerDynamic(ev, communication);
                ev.AddEventHandler(instance, eventHandler);
            }
            catch (Exception ex)
            {            }
        }
    }

    private Delegate CreateEventHandlerDynamic(EventInfo ev, IDataCommunication communication)
    {
        var handlerType = ev.EventHandlerType;
        var invokeMethod = handlerType.GetMethod("Invoke");
        var invokeParams = invokeMethod.GetParameters();

        var parameterExpressions = invokeParams
            .Select(p => Expression.Parameter(p.ParameterType, p.Name))
            .ToArray();

        var logMethod = GetType().GetMethod(nameof(LogEventGeneric), BindingFlags.NonPublic | BindingFlags.Instance);

        var argsArrayExpr = Expression.NewArrayInit(
            typeof(object),
            parameterExpressions.Select(pe => Expression.Convert(pe, typeof(object)))
        );

        var communicationExpr = Expression.Constant(communication, typeof(IDataCommunication));

        var eventInfo = Expression.Constant(ev, typeof(EventInfo));

        var callExpr = Expression.Call(
            Expression.Constant(this),
            logMethod,
            argsArrayExpr,
            communicationExpr,
            eventInfo
        );

        var lambda = Expression.Lambda(handlerType, callExpr, parameterExpressions);
        return lambda.Compile();
    }

    private void LogEventGeneric(object[] args, IDataCommunication communication, EventInfo eventInfo)
    {
        RaiseEvent(communication, $"{eventInfo.DeclaringType.FullName}.{eventInfo.Name}", eventInfo.Name, eventInfo.Module.Name, args);
    }


    private string LoadConfig(string configFilePath)
    {
        if (!File.Exists(configFilePath))
        {
            throw new FileNotFoundException($"Config file '{configFilePath}' not found.");
        }

        configFilePath = Path.GetFullPath(configFilePath);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Path.GetDirectoryName(configFilePath) ?? throw new InvalidOperationException("Invalid config file path."))
            .AddJsonFile(Path.GetFileName(configFilePath))
            .Build();

        return configuration["LibrariesPath"] ?? throw new InvalidOperationException("AssembliesPath not configured in appsettings.");
    }


    private void AdjustParameterTypes(object[] parameters, ParameterInfo[] targetParameters)
    {
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i] != null && parameters[i].GetType() != targetParameters[i].ParameterType)
            {
                parameters[i] = Convert.ChangeType(parameters[i], targetParameters[i].ParameterType);
            }
        }
    }

    public class RuntimeEventArgs : EventArgs
    {
        public string Source { get; set; }
        public string EventName { get; set; }
        public string Message { get; set; }
        public dynamic Data { get; set; }
    }

    public class AssemblyDescription
    {
        public string FullName { get; set; }
        public List<TypeDescription> Types { get; set; } = new();
    }

    public class TypeDescription
    {
        public string Name { get; set; }
        public List<MethodDescription> Methods { get; set; } = new();
        public List<EventDescription> Events { get; set; } = new();
        public List<FieldDescription> Fields { get; set; } = new();
    }

    public class MethodDescription
    {
        public string Name { get; set; }
        public string ReturnType { get; set; }
        public List<string> Parameters { get; set; } = new();
    }

    public class EventDescription
    {
        public string Name { get; set; }
        public string EventHandlerType { get; set; }
    }

    public class FieldDescription
    {
        public string Name { get; set; }
        public string FieldType { get; set; }
    }

    public class MethodCall
    {
        public string Assembly { get; set; }
        public string Class { get; set; }
        public string Method { get; set; }
        public dynamic[] ConstructorParameters { get; set; }
        public dynamic[] Parameters { get; set; }
    }
}
