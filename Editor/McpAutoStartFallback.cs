using System;
using System.Reflection;
using UnityEditor;

// Religa a ponte do MCP for Unity quando o auto-start do pacote se perde.
// Medido em 16/10: o HttpAutoStartHandler do pacote agenda o start por EditorApplication.delayCall e marca em
// SessionState que ja rodou; se o editor recompila scripts logo na abertura (reload de dominio), o delayCall some e a
// marca fica, entao a ponte nunca sobe nessa sessao (porta 8080 fechada, sem nenhuma linha MCP no log). Aqui, alguns
// segundos depois de cada reload, chamamos o mesmo OnEditorReady do pacote (por reflexao: a classe e interna); ele
// e idempotente (sai se a ponte ja esta de pe ou se o auto-start esta desligado nas preferencias).
[InitializeOnLoad]
internal static class McpAutoStartFallback
{
    private const double DelaySeconds = 4.0;
    private static double startAt;

    static McpAutoStartFallback()
    {
        if (!EditorPrefs.GetBool("MCPForUnity.AutoStartOnLoad", false)) return;
        startAt = EditorApplication.timeSinceStartup + DelaySeconds;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (EditorApplication.timeSinceStartup < startAt || EditorApplication.isCompiling) return;
        EditorApplication.update -= Tick;
        try
        {
            Type handler = null;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                handler = asm.GetType("MCPForUnity.Editor.Services.HttpAutoStartHandler");
                if (handler != null) break;
            }
            var ready = handler?.GetMethod("OnEditorReady", BindingFlags.Static | BindingFlags.NonPublic);
            ready?.Invoke(null, null);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogWarning("McpAutoStartFallback: " + e.Message);
        }
    }
}

// touched 2026-10-18 07:40 to force a domain reload after the power cut (the bridge reconnects on reload)
