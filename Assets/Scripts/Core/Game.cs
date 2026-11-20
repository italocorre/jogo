using UnityEngine;

namespace MarAberto
{
    /// <summary>Monta o mundo e guarda as referências principais.</summary>
    public class Game : MonoBehaviour
    {
        public static Game Instance { get; private set; }

        public Inventory Inventory { get; private set; }
        public PlayerController Player { get; private set; }
        public Pickaxe Pickaxe { get; private set; }
        public Hud Hud { get; private set; }
        public WorldEnvironment Environment { get; private set; }
        public Island StartIsland { get; private set; }

        AudioSource oceanAudio;
        AudioLowPassFilter lowPass;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 144;
            Inventory = new Inventory();

            // Remove câmeras/luzes que porventura existam na cena.
            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) Destroy(c.gameObject);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) Destroy(l.gameObject);

            // Ilha inicial
            var islandGo = new GameObject("Ilha do Porto");
            StartIsland = islandGo.AddComponent<Island>();
            StartIsland.Generate();

            // Jogador
            var playerGo = new GameObject("Jogador");
            playerGo.AddComponent<CharacterController>();
            Player = playerGo.AddComponent<PlayerController>();
            Player.Init(StartIsland.SpawnPoint, StartIsland.SpawnLook);
            Pickaxe = playerGo.AddComponent<Pickaxe>();
            Pickaxe.Init(Player);

            // Ambiente e mar
            Environment = gameObject.AddComponent<WorldEnvironment>();
            Environment.Init(Player.Cam);
            var oceanGo = new GameObject("Mar");
            oceanGo.AddComponent<Ocean>().Init(Player.Cam.transform);

            // Som ambiente do mar
            oceanAudio = Player.Cam.gameObject.AddComponent<AudioSource>();
            oceanAudio.clip = Sfx.OceanLoop;
            oceanAudio.loop = true;
            oceanAudio.volume = 0.35f;
            oceanAudio.spatialBlend = 0f;
            oceanAudio.Play();
            lowPass = Player.Cam.gameObject.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 22000f;

            // Interface
            Hud = gameObject.AddComponent<Hud>();
            Hud.Init(this);
            Hud.Toast("Bem-vindo à Ilha do Porto!", new Color(1f, 0.9f, 0.6f));
        }

        void Update()
        {
            // Som abafado debaixo d'água.
            bool under = Environment.CameraUnderwater;
            lowPass.cutoffFrequency = Mathf.Lerp(lowPass.cutoffFrequency, under ? 700f : 22000f, Time.deltaTime * 8f);
            oceanAudio.volume = Mathf.Lerp(oceanAudio.volume, under ? 0.55f : 0.35f, Time.deltaTime * 4f);
        }
    }
}
