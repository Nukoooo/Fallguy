using System.Collections.Generic;
using System.Linq;
using Dalamud.Interface.Windowing;
using ImGuiNET;
using Microsoft.Extensions.DependencyInjection;

namespace FallGuy.Windows;

internal class MainWindow : Window
{
    private readonly List<IUiModule> _modules;
    private int _selectedTab;

    public MainWindow(ServiceProvider service) : base("糖豆人挂机")
    {
        _modules = service.GetServices<IUiModule>().Where(i =>
        {
            if (!i.ShouldDrawUi)
                return false;
            if (!string.IsNullOrWhiteSpace(i.UiName)) 
                return true;
            DalamudApi.PluginLog.Warning($"{i.GetType().Name} should draw ui but doesn't have name for ui");
            return false;
        }).ToList();

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new(400, 250),
            MaximumSize = new(1280, 720),
        };
    }

    public override void Draw()
    {
        DrawTabSelection();
        DrawTab();
    }

    private void DrawTabSelection()
    {
        ImGui.BeginGroup();

        ImGui.BeginChild("Child1##BetterFakeName", new(ImGui.GetFrameHeight() * 4.5f, 0), true);

        for (var i = 0; i < _modules.Count; i++)
        {
            if (ImGui.Selectable(_modules[i].UiName + "##BetterFakeName", i == _selectedTab))
            {
                _selectedTab = i;
            }
        }

        ImGui.EndChild();
    }

    private void DrawTab()
    {
        ImGui.SameLine();
        ImGui.BeginChild("Child2##BetterFakeName", new(-1, -1), true);

        _modules[_selectedTab].OnDrawUi();

        ImGui.EndChild();
        ImGui.EndGroup();
    }
}