using UnityEngine;

namespace MarAberto
{
    /// <summary>
    /// Ponto de entrada. O jogo inteiro é montado por código quando a cena carrega,
    /// então não é preciso montar nada no editor da Unity.
    /// </summary>
    public static class Bootstrap
    {
        static bool started;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (started) return;
            started = true;
            // Primeiro o menu (sozinho / hospedar / entrar); ele cria o Game.
            var go = new GameObject("Menu");
            go.AddComponent<MainMenu>();
        }
    }
}
