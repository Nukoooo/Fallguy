using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using Dalamud.Hooking;
using FallGuy.Extensions;
using FFXIVClientStructs.FFXIV.Client.Game.Control;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;

namespace FallGuy.Modules;

internal unsafe class AFK(Configuration configuration) : IUiModule
{
    private LeaveDutyDelegate _leaveDungeon;

    private delegate void LeaveDutyDelegate(byte isTimeout);

    private delegate void PublicContentFallGuyUpdateDelegate(nint a1, int a2, uint a3, uint a4, uint a5);

    private Hook<PublicContentFallGuyUpdateDelegate>? Hook { get; set; }

    public bool Init()
    {
        if (!DalamudApi.SigScanner.TryScanText("40 53 48 83 EC ?? 48 8B 05 ?? ?? ?? ?? 0F B6 D9 48 85 C0", out var address))
        {
            return false;
        }

        _leaveDungeon = Marshal.GetDelegateForFunctionPointer<LeaveDutyDelegate>(address);

        if (!DalamudApi.SigScanner.TryScanText("48 89 5C 24 ?? 57 48 83 EC ?? FF CA 41 8B D8", out address))
        {
            return false;
        }

        Hook = DalamudApi.GameInterop.HookFromAddress<PublicContentFallGuyUpdateDelegate>(address,
                 hk_PublicContentFallGuyUpdate);

        Hook.Enable();

        DalamudApi.Condition.ConditionChange += OnConditionChange;

        return true;
    }

    private void OnConditionChange(ConditionFlag flag, bool value)
    {
        if (!configuration.Enabled || DalamudApi.ClientState.TerritoryType != 1197)
        {
            return;
        }

        if (flag == ConditionFlag.BetweenAreas51 && !value)
        {
            LookUpAndInteractWithObject();
        }
    }

    public void Shutdown()
    {
        DalamudApi.Condition.ConditionChange -= OnConditionChange;

        Hook?.Disable();
        Hook?.Dispose();
    }

    public string UiName => "挂!";

    public void OnDrawUi()
    {
        var enabled = configuration.Enabled;

        if (ImGui.Checkbox("启用##目标信息", ref enabled))
        {
            configuration.Enabled = enabled;

            if (enabled)
            {
                LookUpAndInteractWithObject();
            }

            configuration.Save();
        }

        ImGui.BeginDisabled(!configuration.Enabled);

        var delay = configuration.Delay;

        if (ImGui.SliderInt("退本延迟(毫秒)", ref delay, 0, 5000))
        {
            configuration.Delay = delay;
            configuration.Save();
        }

        ImGui.EndDisabled();

        ImGui.TextUnformatted("注意事项:");
        ImGui.TextUnformatted("自动确认进本需要自行解决");
        ImGui.TextUnformatted("如果和目标互动失败会自动关闭");
    }

    private void hk_PublicContentFallGuyUpdate(nint a1, int type, uint a3, uint a4, uint a5)
    {
        Hook?.Original(a1, type, a3, a4, a5);

        if (!configuration.Enabled)
        {
            return;
        }

        if (type != 1)
        {
            return;
        }

        var mapType = (a3 >> 20) & 0xF;

        // 等待大厅
        if (mapType == 5)
        {
            return;
        }

        var status = a3.GetHighByte() & 0xF;

        // 0是在等待玩家 1是等待加载完毕 2是准备开始 3是正在进行 4是结束当前地图 5是结束游戏
        if (status != 4)
        {
            return;
        }

        DalamudApi.Framework.RunOnTick(() => { _leaveDungeon(1); },
                                       TimeSpan.FromMilliseconds(configuration.Delay));

        /*var stage   = a3 >> 28;*/
        DalamudApi.PluginLog.Info($"Status: {status}, a1 + 0x1178: {*(int*) (a1   + 0x1178)}");
        DalamudApi.PluginLog.Info($"MapType: {mapType}, a1 + 0x1188: {*(int*) (a1 + 0x1188)}");
    }

    private void LookUpAndInteractWithObject()
    {
        var targetObj = DalamudApi.ObjectTable.FirstOrDefault(i => i.DataId == 0xFF7A8);

        var targetSystem = TargetSystem.Instance();

        if (targetObj == null
            || DalamudApi.ClientState.LocalPlayer is not { } local
            || Math.Sqrt(Math.Pow(local.Position.X   - targetObj.Position.X, 2)
                         + Math.Pow(local.Position.Z - targetObj.Position.Z, 2))
            > 26.999)
        {
            configuration.Enabled = false;

            Print("和目标《节目登记员》交互失败。找不到目标或者不在交互范围内，已自动关闭");

            UIGlobals.PlayChatSoundEffect(6);
            UIGlobals.PlayChatSoundEffect(6);
            UIGlobals.PlayChatSoundEffect(6);

            return;
        }

        targetSystem->InteractWithObject((GameObject*) targetObj.Address, false);
    }

    private static void Print(string text)
    {
        var payloads = new List<Payload>
        {
            new UIForegroundPayload(1),
            new TextPayload("["),
            new UIForegroundPayload(35),
            new TextPayload("糖豆人挂机"),
            new UIForegroundPayload(0),
            new TextPayload("] "),
            new TextPayload(text),
            new UIForegroundPayload(0),
        };

        DalamudApi.ChatGui.Print(new SeString(payloads));
    }
}
