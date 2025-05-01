using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using Debug = UnityEngine.Debug;
using System.Threading;
using System.Collections; // Necessário para Coroutines
using System.Collections.Generic; // Necessário para a lista de estados EWMH
using System.Text; // Necessário para trabalhar com strings em X11 properties


// Assumimos que este script SÓ será executado em builds Linux Standalone (X11)
public class LinuxWallpaper : MonoBehaviour // Mantendo o nome original do seu arquivo
{
    // Variável para o handle da janela Unity
    private IntPtr _handleJanelaUnity = IntPtr.Zero;

    // Variáveis específicas do Linux (X11)
    private IntPtr _displayX11 = IntPtr.Zero;
    private IntPtr _handleTelaRootX11 = IntPtr.Zero;
    private IntPtr _contextoRecordX11 = IntPtr.Zero; // Placeholder
    private Thread _threadHookX11; // Placeholder
    private volatile bool _pararThreadHookX11 = false; // Placeholder

    // Handles de Atoms comuns e EWMH
    private IntPtr _atom_ATOM = IntPtr.Zero;
    private IntPtr _atom_CARDINAL = IntPtr.Zero;
    private IntPtr _atom_NET_WM_PID = IntPtr.Zero;
    private IntPtr _atom_NET_WM_WINDOW_TYPE = IntPtr.Zero;
    private IntPtr _atom_NET_WM_WINDOW_TYPE_DESKTOP = IntPtr.Zero;
    private IntPtr _atom_NET_WM_STATE = IntPtr.Zero;
    private IntPtr _atom_NET_WM_STATE_BELOW = IntPtr.Zero;
    private IntPtr _atom_NET_WM_STATE_STICKY = IntPtr.Zero; // Opcional
    private IntPtr _atom_NET_WORKAREA = IntPtr.Zero;

    // Constantes EWMH (Strings para XInternAtom)
    private const string EWMH_WM_WINDOW_TYPE_STR = "_NET_WM_WINDOW_TYPE";
    private const string EWMH_WM_WINDOW_TYPE_DESKTOP_STR = "_NET_WM_WINDOW_TYPE_DESKTOP";
    private const string EWMH_WM_STATE_STR = "_NET_WM_STATE";
    private const string EWMH_WM_STATE_BELOW_STR = "_NET_WM_STATE_BELOW";
    private const string EWMH_WM_STATE_STICKY_STR = "_NET_WM_STATE_STICKY";
    private const string NET_WM_PID_STR = "_NET_WM_PID";
    private const string NET_WORKAREA_STR = "_NET_WORKAREA";


    // ** DllImports para X11 (libX11.so.6) **

    [DllImport("libX11.so.6")]
    public static extern IntPtr XOpenDisplay(string display_name);

    [DllImport("libX11.so.6")]
    public static extern int XCloseDisplay(IntPtr display);

    [DllImport("libX11.so.6")]
    public static extern IntPtr XDefaultRootWindow(IntPtr display);

    [DllImport("libX11.so.6")]
    public static extern int XQueryTree(
        IntPtr display, IntPtr w,
        out IntPtr root_return, out IntPtr parent_return,
        out IntPtr children_return, out int nchildren_return
    );

    [DllImport("libX11.so.6")]
    public static extern int XFree(IntPtr data);

    // [DllImport("libX11.so.6")] public static extern int XReparentWindow(IntPtr display, IntPtr window, IntPtr parent, int x, int y); // REMOVIDO
    [DllImport("libX11.so.6")]
    public static extern int XFlush(IntPtr display);

    [DllImport("libX11.so.6")]
    public static extern int XMapWindow(IntPtr display, IntPtr w);

    [DllImport("libX11.so.6")]
    public static extern int XMoveResizeWindow(IntPtr display, IntPtr w, int x, int y, int width, int height);

    [DllImport("libX11.so.6")]
    public static extern IntPtr XInternAtom(IntPtr display, string atom_name, bool only_if_exists);

    [DllImport("libX11.so.6")]
    public static extern int XGetWindowProperty(
        IntPtr display, IntPtr w, IntPtr property, IntPtr long_offset, IntPtr long_length, bool _delete,
        IntPtr req_type, out IntPtr actual_type_return, out int actual_format_return, out IntPtr nitems_return,
        out IntPtr bytes_after_return, out IntPtr prop_return
    );

    [DllImport("libX11.so.6")]
    public static extern int XChangeProperty(IntPtr display, IntPtr w, IntPtr property, IntPtr type, int format,
        int mode, ref IntPtr data, int nelements);

    [DllImport("libX11.so.6")]
    public static extern int XChangeProperty(IntPtr display, IntPtr w, IntPtr property, IntPtr type, int format,
        int mode, IntPtr data, int nelements);

    private const int PropModeReplace = 0;

    [StructLayout(LayoutKind.Sequential)]
    public struct XWindowAttributes
    {
        public int x, y;
        public int width, height;
        public int border_width;
        public int depth;
        public IntPtr visual;
        public IntPtr root;
        public int c_class;
        public int bit_gravity;
        public int win_gravity;
        public int backing_store;
        public IntPtr backing_planes;
        public IntPtr backing_pixel;
        public bool save_under;
        public IntPtr colormap;
        public bool map_installed;
        public int map_state;
        public IntPtr all_event_masks;
        public IntPtr do_not_propagate_mask;
        public bool override_redirect;
        public IntPtr screen;
    }

    [DllImport("libX11.so.6")]
    public static extern int XGetWindowAttributes(IntPtr display, IntPtr w, out XWindowAttributes attributes);

    [StructLayout(LayoutKind.Sequential)]
    public struct XRectangle
    {
        public int x, y, width, height;
    }


    // Funções de Eventos e Hooks (Placeholders para XRecord)
    // [DllImport("libXtst.so.6")] public static extern IntPtr XRecordCreateContext(IntPtr display, int /* XRecordInterceptAlso */ protocol_interest, IntPtr /* XRecordRange* */ ranges, int n_ranges);
    // ... etc.


    #region MonoBehaviour Methods

    void Start()
    {
        Debug.Log($"[WallpaperManager] Iniciando no sistema: {Application.platform}...");

        StartCoroutine(ConfigurarLinuxX11ComAtraso(0.5f)); // Mantendo o atraso
    }

    void OnDestroy()
    {
        Debug.Log("[WallpaperManager] OnDestroy chamado. Limpando...");
        if (enabled && _displayX11 != IntPtr.Zero)
        {
            LimparLinuxX11();
        }
    }

    void OnApplicationQuit()
    {
        Debug.Log("[WallpaperManager] OnApplicationQuit chamado. Garantindo limpeza...");
        if (enabled && _displayX11 != IntPtr.Zero)
        {
            LimparLinuxX11();
        }
    }

    #endregion

    // Coroutine para configurar X11 com atraso
    private IEnumerator ConfigurarLinuxX11ComAtraso(float delay)
    {
        yield return new WaitForSeconds(delay);

        Debug.Log($"[WallpaperManager] Atraso de {delay}s concluído. Iniciando configuração X11.");

        string currentDesktop = Environment.GetEnvironmentVariable("XDG_CURRENT_DESKTOP");
        string displayVar = Environment.GetEnvironmentVariable("DISPLAY");
        string waylandDisplay = Environment.GetEnvironmentVariable("WAYLAND_DISPLAY");

        Debug.Log(
            $"[WallpaperManager] XDG_CURRENT_DESKTOP: '{currentDesktop}', DISPLAY: '{displayVar}', WAYLAND_DISPLAY: '{waylandDisplay}'");

        if (!string.IsNullOrEmpty(displayVar) && string.IsNullOrEmpty(waylandDisplay) &&
            (!string.IsNullOrEmpty(currentDesktop) && (currentDesktop.Contains("GNOME") ||
                                                       currentDesktop.Contains("Unity") ||
                                                       currentDesktop.Contains("Cinnamon") ||
                                                       currentDesktop.Contains("XFCE") ||
                                                       currentDesktop.Contains("KDE") ||
                                                       currentDesktop.Contains("MATE") ||
                                                       currentDesktop.Contains("LXQt") ||
                                                       currentDesktop.Contains("Budgie"))))
        {
            Debug.Log("[WallpaperManager] Ambiente Linux X11 compatível detectado.");
            ConfigurarLinuxX11();
        }
        else if (!string.IsNullOrEmpty(waylandDisplay))
        {
            Debug.LogError(
                "[WallpaperManager] Não é possível configurar: Ambiente Wayland detectado. Script desativado.");
            enabled = false;
        }
        else
        {
            Debug.LogError(
                "[WallpaperManager] Não é possível configurar: Ambiente Linux não detectado como X11 compatível ou DISPLAY não definido. Script desativado.");
            enabled = false;
        }
    }


    // --- Implementação Específica para Linux (X11) ---

    private void ConfigurarLinuxX11()
    {
        Debug.LogWarning(
            "[WallpaperManager] Tentando configurar para X11 como wallpaper (sem decorações, sem cobrir painéis).");

        // 1. Abrir conexão com o display X11
        _displayX11 = XOpenDisplay(null);
        if (_displayX11 == IntPtr.Zero)
        {
            Debug.LogError(
                "[WallpaperManager] Não foi possível abrir a conexão com o display X11. Script desativado.");
            enabled = false;
            return;
        }

        Debug.Log($"[WallpaperManager] Conexão com display X11 aberta: {_displayX11}");

        // Obter todos os átomos necessários
        _atom_ATOM = XInternAtom(_displayX11, "ATOM", false);
        _atom_CARDINAL = XInternAtom(_displayX11, "CARDINAL", false);
        _atom_NET_WM_PID = XInternAtom(_displayX11, NET_WM_PID_STR, false);
        _atom_NET_WM_WINDOW_TYPE = XInternAtom(_displayX11, EWMH_WM_WINDOW_TYPE_STR, false);
        _atom_NET_WM_WINDOW_TYPE_DESKTOP = XInternAtom(_displayX11, EWMH_WM_WINDOW_TYPE_DESKTOP_STR, false);
        _atom_NET_WM_STATE = XInternAtom(_displayX11, EWMH_WM_STATE_STR, false);
        _atom_NET_WM_STATE_BELOW = XInternAtom(_displayX11, EWMH_WM_STATE_BELOW_STR, false);
        _atom_NET_WM_STATE_STICKY = XInternAtom(_displayX11, EWMH_WM_STATE_STICKY_STR, false);
        _atom_NET_WORKAREA = XInternAtom(_displayX11, NET_WORKAREA_STR, false);

        if (_atom_ATOM == IntPtr.Zero || _atom_CARDINAL == IntPtr.Zero || _atom_NET_WM_PID == IntPtr.Zero ||
            _atom_NET_WM_WINDOW_TYPE == IntPtr.Zero || _atom_NET_WM_WINDOW_TYPE_DESKTOP == IntPtr.Zero ||
            _atom_NET_WM_STATE == IntPtr.Zero || _atom_NET_WM_STATE_BELOW == IntPtr.Zero ||
            _atom_NET_WORKAREA == IntPtr.Zero)
        {
            Debug.LogError("[WallpaperManager] Falha ao obter um ou mais átomos essenciais do X11. Script desativado.");
            LimparLinuxX11();
            enabled = false;
            return;
        }
        else
        {
            Debug.Log("[WallpaperManager] Átomos essenciais do X11 obtidos com sucesso.");
        }

        // 2. Obter o Handle da Root Window
        _handleTelaRootX11 = ObterHandleTelaRootLinuxX11(_displayX11);
        if (_handleTelaRootX11 == IntPtr.Zero)
        {
            Debug.LogError(
                "[WallpaperManager] Não foi possível obter o Handle da Root Window (Linux/X11). Script desativado.");
            LimparLinuxX11();
            enabled = false;
            return;
        }

        Debug.Log($"[WallpaperManager] Handle da Root Window (XID): {_handleTelaRootX11}");


        // 3. Obter o Handle da Janela Unity (XID)
        _handleJanelaUnity = ObterHandleJanelaUnityLinuxX11(_displayX11);
        if (_handleJanelaUnity == IntPtr.Zero)
        {
            Debug.LogError(
                "[WallpaperManager] Não foi possível obter o Handle da Janela Unity (Linux/X11) via PID. Script desativado.");
            LimparLinuxX11();
            enabled = false;
            return;
        }

        Debug.Log($"[WallpaperManager] Handle da Janela Unity (XID) encontrado: {_handleJanelaUnity}");


        // 4. Ajustar Estilos e Propriedades da Janela (para modo wallpaper sem decorações)
        // Isto tentará definir as propriedades EWMH e redimensionar.
        AjustarEstilosJanelaLinuxX11_Wallpaper(_displayX11, _handleJanelaUnity, _handleTelaRootX11);


        // 5. Reparentamento REMOVIDO - Deixe o Window Manager gerenciar a janela
        // Definir Parentesco para a Root Window não é mais feito nesta versão.
        Debug.Log("[WallpaperManager] Reparentamento para Root Window omitido.");


        // Após definir estilos, garanta que a janela está visível e que os comandos foram enviados
        XMapWindow(_displayX11, _handleJanelaUnity); // Garante que está mapeada (visível)
        XFlush(_displayX11); // Envia todos os comandos pendentes para o servidor X

        // 6. Instalar o Hook de Input (XRecord) - Placeholder
        InstalarHookInputLinuxX11();

        Debug.Log("[WallpaperManager] Tentativa de configuração Linux/X11 concluída. (Sem Reparentamento)");
    }

    private void LimparLinuxX11()
    {
        Debug.Log("[WallpaperManager] Iniciando limpeza para Linux/X11...");

        RemoverHookInputLinuxX11();

        if (_displayX11 != IntPtr.Zero)
        {
            XCloseDisplay(_displayX11);
            Debug.Log("[WallpaperManager] Conexão com display X11 fechada.");
            _displayX11 = IntPtr.Zero;
        }

        // Reset handles e átomos
        _handleJanelaUnity = IntPtr.Zero;
        _handleTelaRootX11 = IntPtr.Zero;
        _contextoRecordX11 = IntPtr.Zero;
        _atom_ATOM = IntPtr.Zero;
        _atom_CARDINAL = IntPtr.Zero;
        _atom_NET_WM_PID = IntPtr.Zero;
        _atom_NET_WM_WINDOW_TYPE = IntPtr.Zero;
        _atom_NET_WM_WINDOW_TYPE_DESKTOP = IntPtr.Zero;
        _atom_NET_WM_STATE = IntPtr.Zero;
        _atom_NET_WM_STATE_BELOW = IntPtr.Zero;
        _atom_NET_WM_STATE_STICKY = IntPtr.Zero;
        _atom_NET_WORKAREA = IntPtr.Zero;

        Debug.Log("[WallpaperManager] Limpeza Linux/X11 concluída.");
    }

    // --- Métodos Auxiliares para Linux/X11 ---

    private IntPtr ObterHandleTelaRootLinuxX11(IntPtr display)
    {
        if (display == IntPtr.Zero) return IntPtr.Zero;
        return XDefaultRootWindow(display);
    }

    private IntPtr ObterHandleJanelaUnityLinuxX11(IntPtr display)
    {
        if (display == IntPtr.Zero) return IntPtr.Zero;

        int currentPid = Process.GetCurrentProcess().Id;
        Debug.Log($"[WallpaperManager] Procurando janela X11 com PID: {currentPid}");

        IntPtr rootWindow = XDefaultRootWindow(display);
        if (rootWindow == IntPtr.Zero)
        {
            Debug.LogError("[WallpaperManager] Não foi possível obter a Root Window para busca de PID.");
            return IntPtr.Zero;
        }

        if (_atom_NET_WM_PID == IntPtr.Zero || _atom_CARDINAL == IntPtr.Zero)
        {
            Debug.LogError(
                $"[WallpaperManager] Átomos '{NET_WM_PID_STR}' ou 'CARDINAL' não disponíveis. A busca por PID não é possível.");
            return IntPtr.Zero;
        }

        return EncontrarJanelaPorPidRecursivo(display, rootWindow, currentPid, _atom_NET_WM_PID, _atom_CARDINAL);
    }

    private IntPtr EncontrarJanelaPorPidRecursivo(IntPtr display, IntPtr currentWindow, int targetPid,
        IntPtr netWmPidAtom, IntPtr cardinalAtom)
    {
        if (currentWindow == IntPtr.Zero || display == IntPtr.Zero) return IntPtr.Zero;

        IntPtr root, parent;
        IntPtr children = IntPtr.Zero;
        int nchildren = 0;

        int queryResult = XQueryTree(display, currentWindow, out root, out parent, out children, out nchildren);

        if (queryResult == 0 || children == IntPtr.Zero)
        {
            if (children != IntPtr.Zero) XFree(children);
            return IntPtr.Zero;
        }

        IntPtr foundWindow = IntPtr.Zero;

        try
        {
            IntPtr[] childHandles = new IntPtr[nchildren];
            if (nchildren > 0) Marshal.Copy(children, childHandles, 0, nchildren);

            foreach (IntPtr child in childHandles)
            {
                IntPtr actualType = IntPtr.Zero;
                int actualFormat = 0;
                IntPtr nitems_ptr = IntPtr.Zero;
                IntPtr bytesAfter_ptr = IntPtr.Zero;
                IntPtr propReturn = IntPtr.Zero;
                IntPtr longOffset = IntPtr.Zero;
                IntPtr longLength = (IntPtr)4; // PID is 1 CARDINAL

                int result = XGetWindowProperty(display, child, netWmPidAtom, longOffset, longLength, false,
                    cardinalAtom, out actualType, out actualFormat, out nitems_ptr, out bytesAfter_ptr,
                    out propReturn);

                if (result == 0 && actualType == cardinalAtom && actualFormat == 32 && nitems_ptr.ToInt32() >= 1 &&
                    propReturn != IntPtr.Zero)
                {
                    int windowPid = Marshal.ReadInt32(propReturn);
                    if (windowPid == targetPid)
                    {
                        foundWindow = child;
                        XFree(propReturn);
                        return foundWindow;
                    }
                }

                if (propReturn != IntPtr.Zero) XFree(propReturn);
            }

            foreach (IntPtr child in childHandles)
            {
                foundWindow = EncontrarJanelaPorPidRecursivo(display, child, targetPid, netWmPidAtom, cardinalAtom);
                if (foundWindow != IntPtr.Zero) return foundWindow;
            }
        }
        finally
        {
            if (children != IntPtr.Zero) XFree(children);
        }

        return IntPtr.Zero;
    }

    // Definir Parentesco Removido nesta versão
    /*
    private void DefinirParentescoLinuxX11(IntPtr display, IntPtr childWindow, IntPtr parentWindow)
    {
         if (display == IntPtr.Zero || childWindow == IntPtr.Zero || parentWindow == IntPtr.Zero) { Debug.LogError("[WallpaperManager] Parâmetros inválidos para DefinirParentescoLinuxX11."); return; }
         Debug.Log($"[WallpaperManager] Tentando definir parentesco X11: Janela {childWindow} -> Pai {parentWindow}");
         int result = XReparentWindow(display, childWindow, parentWindow, 0, 0);
         if (result == 0) { Debug.Log("[WallpaperManager] XReparentWindow chamado com sucesso."); XFlush(display); }
         else { Debug.LogError($"[WallpaperManager] XReparentWindow falhou for window {childWindow}, parent {parentWindow}. Resultado: {result}"); }
    }
    */


    private void AjustarEstilosJanelaLinuxX11_Wallpaper(IntPtr display, IntPtr window, IntPtr rootWindow)
    {
        Debug.Log(
            "[WallpaperManager] AjustarEstilosJanelaLinuxX11_Wallpaper - Definindo propriedades e redimensionando para área útil.");
        if (display == IntPtr.Zero || window == IntPtr.Zero || rootWindow == IntPtr.Zero)
        {
            Debug.LogError("[WallpaperManager] Parâmetros inválidos para AjustarEstilosJanelaLinuxX11_Wallpaper.");
            return;
        }

        if (_atom_ATOM == IntPtr.Zero || _atom_CARDINAL == IntPtr.Zero || _atom_NET_WM_WINDOW_TYPE == IntPtr.Zero ||
            _atom_NET_WM_WINDOW_TYPE_DESKTOP == IntPtr.Zero || _atom_NET_WM_STATE == IntPtr.Zero ||
            _atom_NET_WM_STATE_BELOW == IntPtr.Zero || _atom_NET_WORKAREA == IntPtr.Zero)
        {
            Debug.LogError(
                "[WallpaperManager] Átomos essenciais não disponíveis. Não é possível definir propriedades EWMH e área útil.");
            return;
        }

        // 1. Definir o tipo da janela como DESKTOP
        IntPtr desktopAtomPtr = _atom_NET_WM_WINDOW_TYPE_DESKTOP;
        int resultType = XChangeProperty(display, window, _atom_NET_WM_WINDOW_TYPE, _atom_ATOM, 32, PropModeReplace,
            ref desktopAtomPtr, 1);
        if (resultType == 0)
        {
            Debug.Log($"[WallpaperManager] Propriedade '{EWMH_WM_WINDOW_TYPE_STR}' definida com sucesso.");
        }
        else
        {
            Debug.LogError(
                $"[WallpaperManager] Falha ao definir propriedade '{EWMH_WM_WINDOW_TYPE_STR}'. Resultado: {resultType}");
        }

        // 2. Definir o estado como BELOW (e opcionalmente STICKY)
        System.Collections.Generic.List<IntPtr> desiredStates = new System.Collections.Generic.List<IntPtr>();
        desiredStates.Add(_atom_NET_WM_STATE_BELOW);
        // if(_atom_NET_WM_STATE_STICKY != IntPtr.Zero) desiredStates.Add(_atom_NET_WM_STATE_STICKY);

        if (desiredStates.Count > 0)
        {
            IntPtr statesPtr = Marshal.AllocHGlobal(IntPtr.Size * desiredStates.Count);
            try
            {
                Marshal.Copy(desiredStates.ToArray(), 0, statesPtr, desiredStates.Count);
                int resultState = XChangeProperty(display, window, _atom_NET_WM_STATE, _atom_ATOM, 32,
                    PropModeReplace, statesPtr, desiredStates.Count);
                if (resultState == 0)
                {
                    Debug.Log($"[WallpaperManager] Propriedade '{EWMH_WM_STATE_STR}' definida com sucesso.");
                }
                else
                {
                    Debug.LogError(
                        $"[WallpaperManager] Falha ao definir propriedade '{EWMH_WM_STATE_STR}'. Resultado: {resultState}");
                }
            }
            finally
            {
                if (statesPtr != IntPtr.Zero) Marshal.FreeHGlobal(statesPtr);
            }
        }
        else
        {
            Debug.LogWarning($"[WallpaperManager] Átomos de estado não disponíveis.");
        }

        // 3. Redimensionar para a ÁREA ÚTIL
        IntPtr actualType = IntPtr.Zero;
        int actualFormat = 0;
        IntPtr nitems_ptr = IntPtr.Zero;
        IntPtr bytesAfter_ptr = IntPtr.Zero;
        IntPtr propReturn = IntPtr.Zero;
        IntPtr longLength = (IntPtr)(Marshal.SizeOf<XRectangle>() / 4);

        int resultWorkArea = XGetWindowProperty(display, rootWindow, _atom_NET_WORKAREA, IntPtr.Zero, longLength,
            false, _atom_CARDINAL,
            out actualType, out actualFormat, out nitems_ptr, out bytesAfter_ptr, out propReturn);

        int nitems = nitems_ptr.ToInt32();

        // VERIFICAÇÃO APRIMORADA + LOGS DETALHADOS
        bool workAreaReadSuccessful = (resultWorkArea == 0 && actualType == _atom_CARDINAL && actualFormat == 32 && nitems >= 1 && propReturn != IntPtr.Zero);

        if (workAreaReadSuccessful)
        {
            XRectangle workAreaRect = Marshal.PtrToStructure<XRectangle>(propReturn);
            // VERIFICAÇÃO APRIMORADA: Check for nonsensical dimensions (like 0 or very small)
            if (workAreaRect.width > 100 && workAreaRect.height > 100)
            {
                Debug.Log(
                    $"[WallpaperManager] Obtida _NET_WORKAREA válida: x={workAreaRect.x}, y={workAreaRect.y}, width={workAreaRect.width}, height={workAreaRect.height}");
                XMoveResizeWindow(display, window, workAreaRect.x, workAreaRect.y, workAreaRect.width,
                    workAreaRect.height);
                Debug.Log("[WallpaperManager] Janela redimensionada e movida para a área útil.");
            }
            else // Dados da área útil parecem inválidos apesar do sucesso aparente da chamada
            {
                 Debug.LogWarning(
                    $"[WallpaperManager] _NET_WORKAREA obtida parece inválida apesar da leitura bem-sucedida." +
                    $"\n  Lida: x={workAreaRect.x}, y={workAreaRect.y}, width={workAreaRect.width}, height={workAreaRect.height}" +
                    $"\n  Redimensionando para o tamanho total da Root Window como fallback."
                );
                FallbackResizeToRoot(display, window, rootWindow);
            }
            if (propReturn != IntPtr.Zero) XFree(propReturn); // Always free propReturn if allocated
        }
        else // Falha ao obter _NET_WORKAREA ou dados inválidos (tipo/formato)
        {
            // LOGS DETALHADOS AQUI
             Debug.LogWarning(
                $"[WallpaperManager] Falha ao obter _NET_WORKAREA da Root Window." +
                $"\n  Result XGetWindowProperty: {resultWorkArea}" +
                $"\n  Actual Type: {actualType} (Expected: {_atom_CARDINAL})" + // Compare with expected atom
                $"\n  Actual Format: {actualFormat} (Expected: 32)" +
                $"\n  Items Returned: {nitems}" +
                $"\n  PropReturn is IntPtr.Zero: {propReturn == IntPtr.Zero}" +
                 // Log raw propReturn pointer value if not zero (for advanced debug)
                 (propReturn != IntPtr.Zero ? $"\n  PropReturn (Ptr): {propReturn}" : "") +
                $"\n  Bytes After: {bytesAfter_ptr.ToInt32()}" +
                $"\n  Redimensionando para o tamanho total da Root Window como fallback."
            );

            FallbackResizeToRoot(display, window, rootWindow);
            if (propReturn != IntPtr.Zero) XFree(propReturn); // Always free propReturn if allocated but checks failed
        }

        XFlush(display);
    }

    // Método auxiliar para redimensionamento fallback para Root Window
    private void FallbackResizeToRoot(IntPtr display, IntPtr window, IntPtr rootWindow)
    {
        XWindowAttributes rootAttrs;
        if (XGetWindowAttributes(display, rootWindow, out rootAttrs) != 0)
        {
            Debug.Log(
                $"[WallpaperManager] Redimensionando janela para cobrir Root Window (fallback): {rootAttrs.width}x{rootAttrs.height}");
            XMoveResizeWindow(display, window, 0, 0, rootAttrs.width, rootAttrs.height);
        }
        else
        {
            Debug.LogError("[WallpaperManager] Falha ao obter atributos da Root Window mesmo para fallback.");
        }
    }


    // Placeholder: Instala o hook de input XRecord
    private void InstalarHookInputLinuxX11()
    {
        Debug.LogWarning(
            "[WallpaperManager] InstalarHookInputLinuxX11 (XRecord) NÃO IMPLEMENTADO - Funcionalidade de input wallpaper/global.");
        Debug.Log("[WallpaperManager] Placeholder: Hook de input XRecord não iniciado.");
    }

    // Placeholder: Remove o hook de input XRecord
    private void RemoverHookInputLinuxX11()
    {
        Debug.LogWarning("[WallpaperManager] RemoverHookInputLinuxX11 (XRecord) NÃO IMPLEMENTADA.");
        if (_contextoRecordX11 != IntPtr.Zero)
        {
            Debug.Log("[WallpaperManager] Placeholder: Contexto XRecord (hipotético) liberado.");
            _contextoRecordX11 = IntPtr.Zero;
        }

        if (_threadHookX11 != null && _threadHookX11.IsAlive)
        {
            Debug.Log("[WallpaperManager] Placeholder: Thread XRecord (hipotética) sinalizada para parar.");
            _pararThreadHookX11 = true;
            _threadHookX11 = null;
        }

        _pararThreadHookX11 = false;
        Debug.Log("[WallpaperManager] Placeholder: Limpeza do hook XRecord concluída.");
    }
}