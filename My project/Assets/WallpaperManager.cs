using System;
using System.Collections; // Necessário para Coroutine se usada
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class WallpaperManager : MonoBehaviour
{
    #region Constantes e Enums Windows API

    // Mensagens e Estilos de Janela
    private const uint WM_SPAWN_WORKERW = 0x052C;
    private const int GWL_STYLE = -16;
    private const int GWL_EXSTYLE = -20;
    private const int WS_VISIBLE = 0x10000000;
    private const int WS_POPUP = unchecked((int)0x80000000);
    private const int SW_SHOW = 5;

    // Tipos de Hook e Mensagens de Mouse
    private const int WH_MOUSE_LL = 14; // Low Level Mouse Hook
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;
    private const int WM_RBUTTONDOWN = 0x0204; // Exemplo: adicionar clique direito se necessário
    private const int WM_RBUTTONUP = 0x0205;   // Exemplo
    // Adicione outras mensagens de mouse (WM_MOUSEMOVE, WM_MBUTTONDOWN, etc.) se precisar

    private enum SendMessageTimeoutFlags : uint
    {
        SMTO_NORMAL = 0x0000,
        SMTO_BLOCK = 0x0001,
        SMTO_ABORTIFHUNG = 0x0002,
        SMTO_NOTIMEOUTIFNOTHUNG = 0x0008
    }

    #endregion

    #region Estruturas Windows API

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    [StructLayout(LayoutKind.Sequential)]
    private struct MSLLHOOKSTRUCT
    {
        public POINT pt;
        public uint mouseData; // Informações da roda do mouse ou botões X
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    #endregion

    #region DllImports (User32, Kernel32)
    // Funções de Janela
    [DllImport("user32.dll")] static extern IntPtr FindWindow(string lpClassName, string lpWindowName);
    [DllImport("user32.dll")] static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);
    [DllImport("user32.dll")] static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, SendMessageTimeoutFlags flags, uint timeout, out IntPtr result);
    [DllImport("user32.dll")] static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
    [DllImport("user32.dll")] static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrA", SetLastError = true)] static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll")] static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
    [DllImport("user32.dll")] [return: MarshalAs(UnmanagedType.Bool)] static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam); // Usaremos PostMessage para não bloquear o hook
    // Funções de Hook
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelMouseProc lpfn, IntPtr hMod, uint dwThreadId);
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string lpModuleName);
    #endregion
    #region Variáveis Estáticas e Delegate para Hook
    private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
    private static LowLevelMouseProc _hookProc;
    private static IntPtr _hookID = IntPtr.Zero;
    private static IntPtr _unityWindowHandle = IntPtr.Zero;
    // Delegate para EnumWindows
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    #endregion
    #region MonoBehaviour Methods (Start, OnDestroy)

    void Start()
    {
        Debug.Log("Iniciando Wallpaper Manager com Hook de Mouse...");

        // 1. Encontrar WorkerW (Janela alvo para ser pai)
        IntPtr workerw = FindWorkerW();
        if (workerw == IntPtr.Zero)
        {
            Debug.LogError("WorkerW não encontrado. Abortando.");
            return;
        }
        Debug.Log($"WorkerW encontrado: {workerw}");

        // 2. Obter o Handle da Janela Unity
        _unityWindowHandle = Process.GetCurrentProcess().MainWindowHandle;
        if (_unityWindowHandle == IntPtr.Zero)
        {
             Debug.LogError("Não foi possível obter o Handle da Janela Unity. Abortando.");
            return;
        }
        Debug.Log($"Handle da Janela Unity: {_unityWindowHandle}");
        // 3. Modificar Estilos da Janela Unity (Pop-up, Visível)
        // IMPORTANTE: Manter WS_VISIBLE é essencial. WS_POPUP remove bordas.
        SetWindowLongPtr(_unityWindowHandle, GWL_STYLE, new IntPtr(WS_POPUP | WS_VISIBLE));
        // Limpar estilos estendidos pode ajudar a remover coisas como WS_EX_TRANSPARENT
        SetWindowLongPtr(_unityWindowHandle, GWL_EXSTYLE, IntPtr.Zero);
        // 4. Definir Parentesco (Tornar Unity filho do WorkerW)
        SetParent(_unityWindowHandle, workerw);
        Debug.Log("Parent da Janela Unity definido como WorkerW.");
        // 5. Exibir a Janela
        ShowWindow(_unityWindowHandle, SW_SHOW);
        // 6. Instalar o Hook de Mouse de Baixo Nível
        Debug.Log("Instalando Hook de Mouse (WH_MOUSE_LL)...");
        _hookProc = HookCallback; // Armazena a referência ao delegate
        using (Process curProcess = Process.GetCurrentProcess())
        using (ProcessModule curModule = curProcess.MainModule)
        {
            _hookID = SetWindowsHookEx(WH_MOUSE_LL, _hookProc, GetModuleHandle(curModule.ModuleName), 0);
        }

        if (_hookID == IntPtr.Zero)
        {
            int errorCode = Marshal.GetLastWin32Error();
            Debug.LogError($"Falha ao instalar o hook de mouse. Código de Erro: {errorCode}");
            // Talvez desativar o componente ou tratar o erro
        }
        else
        {
            Debug.Log($"Hook de Mouse instalado com sucesso. ID: {_hookID}");
        }

        Debug.Log("Configuração do Wallpaper concluída.");
    }

    void OnDestroy()
    {
        Debug.Log("Destruindo Wallpaper Manager...");

        // Remover o Hook de Mouse é CRUCIAL
        if (_hookID != IntPtr.Zero)
        {
            Debug.Log($"Removendo Hook de Mouse ID: {_hookID}");
            if (!UnhookWindowsHookEx(_hookID))
            {
                 int errorCode = Marshal.GetLastWin32Error();
                 Debug.LogError($"Falha ao remover o hook de mouse. Código de Erro: {errorCode}");
            }
            _hookID = IntPtr.Zero;
        }
         _hookProc = null; // Limpa a referência ao delegate
    }

    #endregion

    #region Lógica do Hook de Mouse

    private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        // Processar apenas se nCode for válido
        if (nCode >= 0)
        {
            // Verificar se é um evento de clique que nos interessa
            if (wParam == (IntPtr)WM_LBUTTONDOWN || wParam == (IntPtr)WM_LBUTTONUP ||
                wParam == (IntPtr)WM_RBUTTONDOWN || wParam == (IntPtr)WM_RBUTTONUP) // Adicione outros se necessário
            {
                // Obter informações detalhadas do evento de mouse
                MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                POINT clickPoint = hookStruct.pt; // Coordenadas do clique na TELA

                // Obter o retângulo da janela Unity na TELA
                if (_unityWindowHandle != IntPtr.Zero && GetWindowRect(_unityWindowHandle, out RECT unityRect))
                {
                    // Verificar se o ponto do clique está DENTRO do retângulo da janela Unity
                    if (clickPoint.X >= unityRect.Left && clickPoint.X < unityRect.Right && // Usar < Right/Bottom para ser mais preciso
                        clickPoint.Y >= unityRect.Top && clickPoint.Y < unityRect.Bottom)
                    {
                        // O clique ocorreu dentro da nossa janela!
                        // Debug.Log($"Hook: Clique ({wParam}) detectado em ({clickPoint.X},{clickPoint.Y}) DENTRO da janela Unity ({unityRect.Left},{unityRect.Top})-({unityRect.Right},{unityRect.Bottom})");

                        // *** TENTATIVA DE REPASSAR A MENSAGEM PARA A JANELA UNITY ***
                        // PostMessage é geralmente preferível a SendMessage dentro de um hook
                        // para evitar bloqueios e timeouts.
                        // Passamos os parâmetros originais wParam e lParam. O Windows e/ou Unity
                        // podem conseguir interpretar as coordenadas de tela (lParam) corretamente.
                        // Se isso não funcionar, o próximo passo seria converter lParam para coordenadas
                        // relativas ao cliente (janela) antes de postar.
                        PostMessage(_unityWindowHandle, (uint)wParam.ToInt32(), wParam, lParam);

                        // Opcional: Impedir que a mensagem continue (PODE QUEBRAR OUTRAS COISAS!)
                        // Se o PostMessage funcionar para o Unity, talvez seja seguro impedir
                        // que o clique chegue também ao Explorer (ícones da área de trabalho).
                        // Use com MUITO cuidado e teste extensivamente.
                        // return new IntPtr(1); // <-- Descomente com cautela extrema
                    }
                    // else { Debug.Log($"Hook: Clique ({wParam}) detectado em ({clickPoint.X},{clickPoint.Y}) FORA da janela Unity."); }
                }
            }
        }

        // *** ESSENCIAL: Chamar o próximo hook na cadeia ***
        // Se você não fizer isso, bloqueará TODOS os eventos de mouse para outras aplicações.
        return CallNextHookEx(_hookID, nCode, wParam, lParam);
    }


    #endregion

    #region Funções Auxiliares

    // Função para encontrar a janela WorkerW correta
    private IntPtr FindWorkerW()
    {
        IntPtr workerw = IntPtr.Zero;
        IntPtr progman = FindWindow("Progman", null);
        // Cria o WorkerW se não existir
        SendMessageTimeout(progman, WM_SPAWN_WORKERW, IntPtr.Zero, IntPtr.Zero, SendMessageTimeoutFlags.SMTO_NORMAL, 1000, out _);

        // Enumera as janelas para encontrar o WorkerW correto (atrás do SHELLDLL_DefView)
        EnumWindows((topHandle, _) =>
        {
            IntPtr shellView = FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);
            if (shellView != IntPtr.Zero)
            {
                // Encontra o WorkerW que é irmão do que contém SHELLDLL_DefView
                workerw = FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
            }
            return true; // Continua a enumeração
        }, IntPtr.Zero);
        
        return workerw;
    }
    #endregion
}