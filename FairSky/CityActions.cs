using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace FairSky;

internal static class CityActions
{
    public static IContextItem[] For(City city, CityStore store, Action? afterChange = null)
    {
        var actions = new List<IContextItem>();
        if (store.Contains(city))
        {
            actions.Add(Item("Удалить из моих городов", "\uE74D", () => store.Remove(city), afterChange));
            var cities = store.Snapshot();
            int index = Array.FindIndex(cities, c => CityStore.Key(c) == CityStore.Key(city));
            if (index > 0) actions.Add(Item("Переместить выше", "\uE74A", () => store.Move(city, -1), afterChange));
            if (index >= 0 && index < cities.Length - 1) actions.Add(Item("Переместить ниже", "\uE74B", () => store.Move(city, 1), afterChange));
        }
        else actions.Add(Item("Добавить в мои города", "\uE710", () => store.Add(city), afterChange));
        return actions.ToArray();
    }

    private static CommandContextItem Item(string name, string icon, Func<string> action, Action? afterChange) =>
        new(new CityActionCommand(name, icon, action, afterChange));
}

internal sealed partial class CityActionCommand : InvokableCommand
{
    private readonly Func<string> _action;
    private readonly Action? _afterChange;
    public CityActionCommand(string name, string icon, Func<string> action, Action? afterChange)
    {
        Name = name;
        Icon = new IconInfo(icon);
        _action = action;
        _afterChange = afterChange;
    }
    public override ICommandResult Invoke()
    {
        try
        {
            var message = _action();
            _afterChange?.Invoke();
            return CommandResult.ShowToast(message);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or ArgumentException)
        {
            return CommandResult.ShowToast("Не удалось сохранить изменения: " + ex.Message);
        }
    }
}
