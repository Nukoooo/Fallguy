using System;
using System.Linq;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using FallGuy.Managers;
using FallGuy.Modules;
using FallGuy.Windows;
using Microsoft.Extensions.DependencyInjection;

namespace FallGuy;

public sealed class EntryPoint : IDalamudPlugin
{
    private readonly Configuration _configuration;
    private readonly MainWindow _mainWindow;
    private readonly ServiceProvider _serviceProvider;
    private readonly WindowSystem _windowSystem;

    public EntryPoint(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.Create<DalamudApi>();

        _configuration = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();

        var services = new ServiceCollection();
        services.AddSingleton(_configuration);
        ConfigureServices(services);

        _serviceProvider = services.BuildServiceProvider();

        InitModule<IModule>();
        InitModule<IUiModule>();
        PostInitModule<IModule>();
        PostInitModule<IUiModule>();

        _windowSystem = new(typeof(EntryPoint).AssemblyQualifiedName);
        _mainWindow = new(_serviceProvider);

        _windowSystem.AddWindow(_mainWindow);

        pluginInterface.UiBuilder.Draw += UiBuilderOnDraw;
        pluginInterface.UiBuilder.OpenMainUi += UiBuilderOnOpenMainUi;
    }

    public void Dispose()
    {
        _windowSystem.RemoveAllWindows();
        _serviceProvider.GetServices<IModule>().ToList().ForEach(x => x.Shutdown());
        _serviceProvider.GetServices<IUiModule>().ToList().ForEach(x => x.Shutdown());
        _configuration.Save();
    }

    private void UiBuilderOnOpenMainUi() => _mainWindow.Toggle();

    private void UiBuilderOnDraw() => _windowSystem.Draw();

    private void ConfigureServices(IServiceCollection services)
    {
        services.ImplSingleton<IModule, ICommandHandler, CommandHandler>();
        services.AddSingleton<IUiModule, TargetInfo>();
    }

    private void InitModule<T>() where T : IModule
    {
        foreach (var service in _serviceProvider.GetServices<T>())
        {
            var serviceName = service.GetType().FullName;
            if (!service.Init())
                throw new($"Failed to init module {serviceName}!");
            DalamudApi.PluginLog.Info($"Module {serviceName} is loaded.");
        }
    }

    private void PostInitModule<T>() where T : IModule
    {
        foreach (var service in _serviceProvider.GetServices<T>())
        {
            try
            {
                service.PostInit(_serviceProvider);
            }
            catch (Exception e)
            {
                DalamudApi.PluginLog.Error(e, $"Error when calling PostInit for module {service.GetType().FullName}");
            }
        }
    }
}

internal static class DependencyInjections
{
    public static void ImplSingleton<TService1, TService2, TImpl>(this IServiceCollection services)
        where TImpl : class, TService1, TService2
        where TService1 : class
        where TService2 : class
    {
        services.AddSingleton<TImpl>();

        services.AddSingleton<TService1>(x => x.GetRequiredService<TImpl>());
        services.AddSingleton<TService2>(x => x.GetRequiredService<TImpl>());
    }
}