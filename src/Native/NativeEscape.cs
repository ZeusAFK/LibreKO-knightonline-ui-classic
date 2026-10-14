using System.Reflection;

namespace KnightOnlineUiClassic.Native;

public static class NativeEscape
{
    private const string OrderField = "escapeOrder";
    private const string ClosablePanelType = "LibreKO.Features.ClosablePanel";

    public static void CloseBeforeTrailing(WorldRoot world, Func<bool> isOpen, Action close, int trailing)
    {
        if (Native.Get<Array>(world, OrderField) is not { } order || Native.ClientType(ClosablePanelType) is not { } panelType) return;
        var entry = Activator.CreateInstance(panelType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new object[] { isOpen, close }, null);
        var grown = Array.CreateInstance(order.GetType().GetElementType()!, order.Length + 1);
        int at = Math.Max(0, order.Length - trailing);
        Array.Copy(order, 0, grown, 0, at);
        grown.SetValue(entry, at);
        Array.Copy(order, at, grown, at + 1, order.Length - at);
        Native.Set(world, OrderField, grown);
    }
}
