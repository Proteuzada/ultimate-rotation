using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Textures;
using Dalamud.Interface.Textures.TextureWraps;

namespace UltimateRotation.Windows;

internal static class ActionIconButton
{
    public static bool Draw(string id, uint iconId, string name, bool enabled, bool selected, float size)
    {
        var pressed = ImGui.InvisibleButton(id, new Vector2(size, size));
        var min = ImGui.GetItemRectMin();
        var max = ImGui.GetItemRectMax();
        var draw = ImGui.GetWindowDrawList();
        var hovered = ImGui.IsItemHovered();

        draw.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.10f, 0.10f, 0.12f, 1f)), 4f);

        var wrap = Icon(iconId);
        if (wrap is not null)
        {
            draw.AddImage(wrap.Handle, min, max);
            if (!enabled)
                draw.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0f, 0f, 0f, 0.55f)), 4f);
        }
        else
        {
            var letter = string.IsNullOrEmpty(name) ? "?" : name[..1];
            draw.AddText(min + new Vector2(size * 0.32f, size * 0.28f), ImGui.GetColorU32(Vector4.One), letter);
        }

        var border = selected
            ? new Vector4(0.96f, 0.78f, 0.32f, 1f)
            : hovered
                ? new Vector4(1f, 1f, 1f, 0.55f)
                : new Vector4(1f, 1f, 1f, 0.12f);
        draw.AddRect(min, max, ImGui.GetColorU32(border), 4f, ImDrawFlags.None, selected ? 2f : 1f);
        DrawMark(max, enabled);
        return pressed;
    }

    public static void DrawImage(uint iconId, float size)
    {
        var min = ImGui.GetCursorScreenPos();
        ImGui.Dummy(new Vector2(size, size));
        var max = min + new Vector2(size, size);
        var draw = ImGui.GetWindowDrawList();
        draw.AddRectFilled(min, max, ImGui.GetColorU32(new Vector4(0.10f, 0.10f, 0.12f, 1f)), 6f);
        var wrap = Icon(iconId);
        if (wrap is not null)
            draw.AddImage(wrap.Handle, min, max);
    }

    private static void DrawMark(Vector2 max, bool enabled)
    {
        var draw = ImGui.GetWindowDrawList();
        var radius = 7f;
        var center = new Vector2(max.X - radius - 1f, max.Y - radius - 1f);
        var fill = enabled
            ? new Vector4(0.22f, 0.68f, 0.36f, 1f)
            : new Vector4(0.72f, 0.28f, 0.24f, 1f);
        draw.AddCircleFilled(center, radius, ImGui.GetColorU32(fill));
        var mark = ImGui.GetColorU32(Vector4.One);
        if (enabled)
        {
            draw.AddLine(center + new Vector2(-3.2f, 0.2f), center + new Vector2(-1f, 2.4f), mark, 1.6f);
            draw.AddLine(center + new Vector2(-1f, 2.4f), center + new Vector2(3.4f, -2.2f), mark, 1.6f);
        }
        else
        {
            draw.AddLine(center + new Vector2(-2.6f, -2.6f), center + new Vector2(2.6f, 2.6f), mark, 1.6f);
            draw.AddLine(center + new Vector2(2.6f, -2.6f), center + new Vector2(-2.6f, 2.6f), mark, 1.6f);
        }
    }

    private static IDalamudTextureWrap? Icon(uint iconId)
    {
        if (iconId == 0)
            return null;

        try
        {
            return Plugin.TextureProvider.GetFromGameIcon(new GameIconLookup(iconId)).GetWrapOrDefault();
        }
        catch
        {
            return null;
        }
    }
}
