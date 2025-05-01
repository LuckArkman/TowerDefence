﻿using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using UnityEngine;
using Debug = UnityEngine.Debug; // Para evitar conflito com System.Diagnostics.Debug

namespace TowerDefence.Scripts // Mantenha seu namespace
{
    public class WallpaperManager : MonoBehaviour
    {
        #region Constantes e Enums Windows API

        // Mensagens e Estilos de Janela
        private const uint WM_SPAWN_WORKERW = 0x052C;
        private const int GWL_STYLE = -16;
        private const int GWL_EXSTYLE = -20;
        private const int WS_VISIBLE = 0x10000000;
        private const int WS_POPUP = unchecked((int)0x80000000);
        private const int SW_SHOW = 5; // Mostrar e ativar a janela

        // Tipos de Hook e Mensagens de Mouse
        private const int WH_MOUSE_LL = 14;       // Low Level Mouse Hook
        private const uint WM_MOUSEMOVE = 0x0200; // Adicionando movimento se necessário
        private const uint WM_LBUTTONDOWN = 0x0201;
        private const uint WM_LBUTTONUP = 0x0202;
        private const uint WM_RBUTTONDOWN = 0x0204;
        private const uint WM_RBUTTONUP = 0x0205;
        // Adicione outras mensagens (WM_MBUTTONDOWN, WM_MOUSEWHEEL, etc.) se precisar

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
            public POINT pt;          // Coordenadas do mouse (coordenadas de TELA)
            public uint mouseData;    // Dados da roda ou botões X
            public uint flags;
            public uint time;
            public IntPtr dwExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

        #endregion

        #region DllImports (User32, Kernel32)

        // Funções de Janela
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)] // Usar Unicode e SetLastError
        static extern IntPtr FindWindow(string lpClassName, string lpWindowName);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string className, string windowTitle);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, SendMessageTimeoutFlags flags, uint timeout, out IntPtr result);

        [DllImport("user32.dll")]
        static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        // Usar SetWindowLongPtr para compatibilidade x64
        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern IntPtr SetWindowLong32(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        // Wrapper para SetWindowLongPtr que funciona em x86 e x64
        public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            if (IntPtr.Size == 8) // 64 bits
                return SetWindowLongPtr64(hWnd, nIndex, dwNewLong);
            else // 32 bits
                return SetWindowLong32(hWnd, nIndex, dwNewLong);
        }


        [DllImport("user32.dll", SetLastError = true)]
        static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

        // Usaremos PostMessage para não bloquear o hook
        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)] // PostMessage retorna bool
        static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        // Função para converter coordenadas de Tela para Cliente
        [DllImport("user32.dll")]
        static extern bool ScreenToClient(IntPtr hWnd, ref POINT lpPoint);

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

        #region Variáveis Estáticas e Delegates

        private delegate IntPtr LowLevelMouseProc(int nCode, IntPtr wParam, IntPtr lParam);
        private static LowLevelMouseProc _hookProc; // Precisa ser estático ou membro para não ser coletado pelo GC
        private static IntPtr _hookID = IntPtr.Zero;
        private static IntPtr _unityWindowHandle = IntPtr.Zero;
        private static IntPtr _workerW = IntPtr.Zero; // Guardar o handle do WorkerW

        // Delegate para EnumWindows
        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        #endregion

        #region MonoBehaviour Methods

        void Start()
        {
            Debug.Log("[WallpaperManager] Iniciando...");

            // 1. Encontrar WorkerW (Janela alvo para ser pai)
            _workerW = FindWorkerW();
            if (_workerW == IntPtr.Zero)
            {
                Debug.LogError("[WallpaperManager] WorkerW não encontrado. A aplicação pode não se comportar como wallpaper. Abortando configuração de parentesco.");
                // Não necessariamente precisamos abortar tudo, a janela pode funcionar normalmente
                // mas não ficará atrás dos ícones. Poderíamos decidir continuar sem o SetParent.
                // Por agora, vamos parar a configuração específica de wallpaper.
                enabled = false; // Desativa o componente
                return;
            }
            Debug.Log($"[WallpaperManager] WorkerW encontrado: Handle={_workerW}");

            // 2. Obter o Handle da Janela Unity (tentar obter o MainWindowHandle)
            try
            {
                // Obter o processo atual
                Process currentProcess = Process.GetCurrentProcess();
                _unityWindowHandle = currentProcess.MainWindowHandle;

                if (_unityWindowHandle == IntPtr.Zero)
                {
                    // Fallback: Tentar encontrar pela classe (UnityWndClass) e título se MainWindowHandle falhar
                     string windowTitle = Application.productName; // Ou o título exato da sua janela
                    _unityWindowHandle = FindWindow("UnityWndClass", windowTitle);

                     if (_unityWindowHandle == IntPtr.Zero) {
                        Debug.LogError("[WallpaperManager] Não foi possível obter o Handle da Janela Unity (nem por MainWindowHandle, nem por FindWindow). Abortando.");
                        enabled = false;
                        return;
                     }
                      Debug.LogWarning("[WallpaperManager] MainWindowHandle era zero. Encontrado via FindWindow(\"UnityWndClass\", ...)");
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[WallpaperManager] Exceção ao obter o handle da janela Unity: {ex.Message}");
                enabled = false;
                return;
            }

            Debug.Log($"[WallpaperManager] Handle da Janela Unity: Handle={_unityWindowHandle}");

            // 3. Modificar Estilos da Janela Unity (Popup, Visível) e Definir Parentesco
            // É crucial fazer isso *antes* de instalar o hook para que GetWindowRect funcione corretamente
            // com a janela já posicionada.
            MakeUnityChildOfWorkerW();

            // 4. Instalar o Hook de Mouse de Baixo Nível
            InstallMouseHook();

            Debug.Log("[WallpaperManager] Configuração inicial concluída.");
        }

        void OnDestroy()
        {
            Debug.Log("[WallpaperManager] OnDestroy chamado. Limpando hook...");
            CleanupHook();
        }

        void OnApplicationQuit()
        {
            Debug.Log("[WallpaperManager] OnApplicationQuit chamado. Garantindo limpeza do hook...");
            CleanupHook();
        }

        #endregion

        #region Configuração da Janela e Parentesco

        private void MakeUnityChildOfWorkerW()
        {
            if (_unityWindowHandle == IntPtr.Zero || _workerW == IntPtr.Zero)
            {
                Debug.LogError("[WallpaperManager] Handles inválidos para definir parentesco.");
                return;
            }

            // Modificar Estilos: Tornar Pop-up (sem bordas) e manter Visível
            // WS_CHILD não é recomendado aqui, pois queremos controle total da posição/tamanho via Unity/Windows
            IntPtr previousStyle = SetWindowLongPtr(_unityWindowHandle, GWL_STYLE, new IntPtr(WS_POPUP | WS_VISIBLE));
            if (previousStyle == IntPtr.Zero) CheckLastError("[WallpaperManager] Falha ao definir GWL_STYLE.");
            else Debug.Log("[WallpaperManager] Estilo da janela (GWL_STYLE) definido para POPUP | VISIBLE.");

            // Limpar estilos estendidos (opcional, mas pode ajudar a remover sobreposições indesejadas)
            IntPtr previousExStyle = SetWindowLongPtr(_unityWindowHandle, GWL_EXSTYLE, IntPtr.Zero);
            if (previousExStyle == IntPtr.Zero && Marshal.GetLastWin32Error() != 0) // Verificar erro apenas se retornou 0
                 CheckLastError("[WallpaperManager] Falha ao limpar GWL_EXSTYLE.");
             else
                 Debug.Log("[WallpaperManager] Estilo estendido da janela (GWL_EXSTYLE) limpo.");


            // Definir Parentesco (Tornar Unity filho do WorkerW)
            IntPtr result = SetParent(_unityWindowHandle, _workerW);
            if (result == IntPtr.Zero) // SetParent retorna o handle do pai anterior. Zero PODE ser ok, mas vamos checar erro.
            {
                 CheckLastError("[WallpaperManager] Falha ao definir Parent da Janela Unity para WorkerW.");
                 // Considerar desativar o script se o parentesco falhar, pois é crucial.
                 // enabled = false;
                 // return;
            }
            else
            {
                Debug.Log("[WallpaperManager] Parent da Janela Unity definido como WorkerW.");
            }

            // Exibir a Janela (forçar atualização)
            if (!ShowWindow(_unityWindowHandle, SW_SHOW))
            {
                 CheckLastError("[WallpaperManager] Falha ao chamar ShowWindow.");
            } else {
                 Debug.Log("[WallpaperManager] ShowWindow chamado para garantir visibilidade.");
            }

             // Opcional: Ajustar o tamanho da janela para cobrir o ecrã desejado
             // Você pode usar Screen.currentResolution ou System.Windows.Forms.Screen (adicionando referência)
             // e SetWindowPos para ajustar o tamanho/posição se necessário.
             // Exemplo: SetWindowPos(_unityWindowHandle, IntPtr.Zero, 0, 0, Screen.width, Screen.height, 0x0040); // SWP_SHOWWINDOW
        }

        #endregion

        #region Lógica do Hook de Mouse

        private void InstallMouseHook()
        {
             if (_hookID != IntPtr.Zero) {
                Debug.LogWarning("[WallpaperManager] Tentativa de instalar hook quando já existe um. Ignorando.");
                return;
             }

            Debug.Log("[WallpaperManager] Instalando Hook de Mouse Global (WH_MOUSE_LL)...");
            _hookProc = HookCallback; // Armazena a referência ao delegate - ESSENCIAL!

            using (Process curProcess = Process.GetCurrentProcess())
            using (ProcessModule curModule = curProcess.MainModule)
            {
                IntPtr moduleHandle = GetModuleHandle(curModule.ModuleName);
                if(moduleHandle == IntPtr.Zero) {
                    CheckLastError("[WallpaperManager] Falha ao obter o Handle do Módulo atual.");
                    _hookProc = null; // Limpa delegate se falhar
                    return;
                }

                // Instalar o hook global (dwThreadId = 0)
                _hookID = SetWindowsHookEx(WH_MOUSE_LL, _hookProc, moduleHandle, 0);
            }

            if (_hookID == IntPtr.Zero)
            {
                CheckLastError("[WallpaperManager] Falha ao instalar o hook de mouse (SetWindowsHookEx). Input não será redirecionado.");
                _hookProc = null; // Limpa a referência do delegate se falhar
            }
            else
            {
                Debug.Log($"[WallpaperManager] Hook de Mouse instalado com sucesso. ID: {_hookID}");
            }
        }

        private void CleanupHook()
        {
            if (_hookID != IntPtr.Zero)
            {
                Debug.Log($"[WallpaperManager] Removendo Hook de Mouse ID: {_hookID}");
                if (!UnhookWindowsHookEx(_hookID))
                {
                    CheckLastError("[WallpaperManager] Falha ao remover o hook de mouse (UnhookWindowsHookEx).");
                }
                _hookID = IntPtr.Zero;
                _hookProc = null; // Limpa a referência ao delegate após remover o hook
                 Debug.Log("[WallpaperManager] Hook de Mouse removido.");
            }
             // else { Debug.Log("[WallpaperManager] Nenhum hook de mouse para remover."); }
        }

        // O Callback do Hook - PRECISA SER ESTÁTICO se _hookProc for estático
        private static IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
        {
            // Processar apenas se nCode for válido e tivermos um handle de janela válido
            if (nCode >= 0 && _unityWindowHandle != IntPtr.Zero)
            {
                // Converter wParam para a mensagem do mouse
                uint mouseMessage = (uint)wParam.ToInt32();

                // Verificar se é um evento de mouse que nos interessa (cliques, talvez movimento)
                if (mouseMessage == WM_LBUTTONDOWN || mouseMessage == WM_LBUTTONUP ||
                    mouseMessage == WM_RBUTTONDOWN || mouseMessage == WM_RBUTTONUP /*|| mouseMessage == WM_MOUSEMOVE*/)
                {
                    // Obter informações detalhadas do evento de mouse
                    MSLLHOOKSTRUCT hookStruct = (MSLLHOOKSTRUCT)Marshal.PtrToStructure(lParam, typeof(MSLLHOOKSTRUCT));
                    POINT screenPoint = hookStruct.pt; // Coordenadas do clique na TELA

                    // Obter o retângulo da janela Unity na TELA
                    if (GetWindowRect(_unityWindowHandle, out RECT unityRect))
                    {
                        // Verificar se o ponto do clique está DENTRO do retângulo da janela Unity
                        if (screenPoint.X >= unityRect.Left && screenPoint.X < unityRect.Right &&
                            screenPoint.Y >= unityRect.Top && screenPoint.Y < unityRect.Bottom)
                        {
                            // --- CONVERSÃO DE COORDENADAS ---
                            POINT clientPoint = screenPoint;
                            if (ScreenToClient(_unityWindowHandle, ref clientPoint))
                            {
                                // Coordenadas convertidas para o sistema de coordenadas da janela Unity
                                // Debug.Log($"[Hook] Clique ({mouseMessage}) em Tela({screenPoint.X},{screenPoint.Y}) -> Cliente({clientPoint.X},{clientPoint.Y}) dentro da Janela Unity.");

                                // *** REPASSAR A MENSAGEM PARA A JANELA UNITY COM COORDENADAS DE CLIENTE ***
                                // Empacotar coordenadas do cliente no lParam (LOWORD=x, HIWORD=y)
                                // Nota: O wParam original (tipo de botão) é mantido. Para mensagens como WM_MOUSEWHEEL,
                                // o wParam conteria o delta da roda, e mouseData em MSLLHOOKSTRUCT também.
                                // Para cliques simples, o wParam original é suficiente.
                                IntPtr newLParam = (IntPtr)((clientPoint.Y << 16) | (clientPoint.X & 0xFFFF));

                                // Usar PostMessage para enviar a mensagem de forma assíncrona
                                PostMessage(_unityWindowHandle, mouseMessage, wParam, newLParam);

                                // !! CUIDADO !! Descomentar a linha abaixo impedirá que o clique
                                // chegue aos ícones da área de trabalho ou outras janelas por baixo.
                                // Use apenas se tiver certeza que NENHUMA outra interação é desejada.
                                // return new IntPtr(1); // <-- Interrompe a cadeia de hooks para este evento
                            }
                            else
                            {
                                // Falha na conversão de coordenadas (improvável, mas possível)
                                // CheckLastError("[Hook] Falha ao converter ScreenToClient.");
                                // Poderíamos tentar postar as coordenadas de tela originais como fallback?
                                // PostMessage(_unityWindowHandle, mouseMessage, wParam, lParam); // Fallback (menos provável de funcionar bem)
                            }
                        }
                        // else { Debug.Log($"[Hook] Clique ({mouseMessage}) em Tela({screenPoint.X},{screenPoint.Y}) FORA da Janela Unity."); }
                    }
                    // else { CheckLastError("[Hook] Falha ao obter GetWindowRect da Janela Unity."); }
                }
            }

            // *** ESSENCIAL: Chamar o próximo hook na cadeia ***
            // Se não chamar, bloqueia TODOS os eventos de mouse para outras aplicações.
            return CallNextHookEx(_hookID, nCode, wParam, lParam);
        }
        #endregion

        #region Funções Auxiliares

        // Função para encontrar a janela WorkerW correta
        // Esta lógica é padrão e geralmente funciona bem no Win10/11
        private IntPtr FindWorkerW()
        {
            IntPtr workerw = IntPtr.Zero;
            IntPtr progman = FindWindow("Progman", null);

            if (progman == IntPtr.Zero)
            {
                Debug.LogError("[WallpaperManager] Janela 'Progman' não encontrada.");
                return IntPtr.Zero;
            }

            // Cria o WorkerW enviando a mensagem para Progman.
            // Resultado não é diretamente usado, apenas garante que WorkerW exista.
            SendMessageTimeout(progman, WM_SPAWN_WORKERW, IntPtr.Zero, IntPtr.Zero, SendMessageTimeoutFlags.SMTO_NORMAL, 1000, out IntPtr resultMsg);
            if (resultMsg == IntPtr.Zero && Marshal.GetLastWin32Error() != 0)
                 CheckLastError("[WallpaperManager] SendMessageTimeout(WM_SPAWN_WORKERW) pode ter falhado, mas continuaremos tentando encontrar WorkerW.");


            // Enumera as janelas para encontrar o WorkerW correto (atrás do SHELLDLL_DefView)
            EnumWindows((topHandle, _) =>
            {
                // Procurar uma janela filha chamada "SHELLDLL_DefView"
                IntPtr shellView = FindWindowEx(topHandle, IntPtr.Zero, "SHELLDLL_DefView", null);

                // Se encontrou "SHELLDLL_DefView", então o irmão *anterior* (ou às vezes o pai)
                // dessa janela que também é "WorkerW" é o que queremos.
                if (shellView != IntPtr.Zero)
                {
                    // Encontra o WorkerW que é o pai direto do SHELLDLL_DefView ou um irmão dele.
                    // Normalmente é o pai que queremos para SetParent.
                    // Vamos tentar encontrar o WorkerW que é irmão do que contém SHELLDLL_DefView,
                    // como no código original, pois essa é a técnica mais comum.
                    workerw = FindWindowEx(IntPtr.Zero, topHandle, "WorkerW", null);
                    if (workerw != IntPtr.Zero) {
                         // Debug.Log($"[WallpaperManager] WorkerW encontrado via EnumWindows: {workerw}");
                         return false; // Para a enumeração, encontramos!
                    }
                }
                return true; // Continua a enumeração
            }, IntPtr.Zero);

            // Adicional: Se a primeira tentativa falhar, às vezes o WorkerW é filho direto do Progman
             if (workerw == IntPtr.Zero)
             {
                 workerw = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);
                 if (workerw != IntPtr.Zero)
                     Debug.LogWarning("[WallpaperManager] WorkerW encontrado como filho direto do Progman.");
             }


            return workerw;
        }

        // Helper para verificar e logar erros da Win32 API
        private static void CheckLastError(string contextMessage)
        {
            int errorCode = Marshal.GetLastWin32Error();
            if (errorCode != 0)
            {
                Debug.LogError($"{contextMessage} - Erro Win32: {errorCode} - {new System.ComponentModel.Win32Exception(errorCode).Message}");
            }
        }

        #endregion
    }
}