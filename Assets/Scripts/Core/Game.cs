using UnityEngine;

namespace MarAberto
{
    /// <summary>Monta o mundo (também usado pelos prints automáticos do build).</summary>
    public static class WorldBuilder
    {
        public static Island BuildIsland()
        {
            Layers.Setup();
            var islandGo = new GameObject("Ilha do Porto");
            var island = islandGo.AddComponent<Island>();
            island.Generate();
            return island;
        }

        public static Ocean BuildOcean(Transform follow)
        {
            var oceanGo = new GameObject("Mar");
            var o = oceanGo.AddComponent<Ocean>();
            o.Init(follow);
            return o;
        }
    }

    /// <summary>Monta o jogo e guarda as referências principais (moedas, mochila, barco...).</summary>
    public class Game : MonoBehaviour
    {
        public static Game Instance { get; private set; }

        public Inventory Inventory { get; private set; }
        public PlayerController Player { get; private set; }
        public Hands Hands { get; private set; }
        public Hud Hud { get; private set; }
        public WorldEnvironment Environment { get; private set; }
        public Island StartIsland { get; private set; }
        public Ocean Ocean { get; private set; }
        public Boat Boat { get; private set; }
        public int Coins { get; private set; }
        public int BackpackLevel { get; set; }
        public BuildSystem Build { get; private set; }
        public int OxygenLevel { get; set; }

        AudioSource oceanAudio, underAudio, music;
        AudioLowPassFilter lowPass;
        float gullTimer = 8f;
        float musicGap = 20f;
        int musicIndex;
        public float MusicVolume = 0.22f;

        void Awake()
        {
            if (Application.isPlaying) Setup(false);
        }

        /// <summary>Monta o jogo. Com preview=true é usado pelos prints automáticos do build.</summary>
        public void Setup(bool preview)
        {
            Instance = this;
            Application.targetFrameRate = 240; // sem limite prático (VSync desligado)
            Inventory = new Inventory(Inventory.HotbarSize + 12);

            if (!preview)
            {
                foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) Destroy(c.gameObject);
                foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) Destroy(l.gameObject);
            }

            StartIsland = WorldBuilder.BuildIsland();

            var playerGo = new GameObject("Jogador");
            playerGo.AddComponent<CharacterController>();
            Player = playerGo.AddComponent<PlayerController>();
            Player.Init(StartIsland.SpawnPoint, StartIsland.SpawnLook);
            Hands = playerGo.AddComponent<Hands>();
            Hands.Init(Player);

            Environment = gameObject.AddComponent<WorldEnvironment>();
            Environment.Init(Player.Cam);
            Ocean = WorldBuilder.BuildOcean(Player.Cam.transform);

            // Ambiente: ondas, bolhas debaixo d'água e música (música fica fora do filtro "abafado").
            oceanAudio = Sfx.Loop(Player.Cam.gameObject, Sfx.OceanLoop, 0.4f, false);
            underAudio = Sfx.Loop(Player.Cam.gameObject, Sfx.UnderwaterLoop, 0f, false);
            lowPass = Player.Cam.gameObject.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 22000f;
            var musicGo = new GameObject("Musica");
            musicGo.transform.SetParent(transform, false);
            music = musicGo.AddComponent<AudioSource>();
            music.spatialBlend = 0f;
            music.loop = false;
            music.volume = MusicVolume;

            // Começa de mãos vazias: coleta pedras, gravetos e folhas e fabrica as ferramentas no Tab.
            Inventory.Selected = 0;
            Crafting.Init(this);

            Build = gameObject.AddComponent<BuildSystem>();
            Build.Init(this);

            Hud = gameObject.AddComponent<Hud>();
            Hud.Init(this);
        }

        void Start()
        {
            Icons.RenderAll();
            Hud.Toast("Você acordou numa ilha deserta...", Hud.Gold);
            Hud.Toast("Pegue pedras, gravetos e folhas no chão e abra o Tab para fabricar.", Hud.Info);
            musicGap = 6f;
        }

        /// <summary>Atalhos de teste (F1–F6) para testar rápido sem precisar farmar.</summary>
        // Contador de FPS (F7)
        bool showFps;
        float fpsSmooth = 60f;
        GUIStyle fpsStyle;

        void OnGUI()
        {
            if (!showFps) return;
            if (fpsStyle == null) fpsStyle = new GUIStyle { fontSize = 22, normal = { textColor = Color.white } };
            fpsSmooth = Mathf.Lerp(fpsSmooth, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.05f);
            GUI.Label(new Rect(12, Screen.height * 0.5f, 200, 30), Mathf.RoundToInt(fpsSmooth) + " FPS", fpsStyle);
        }

        void TestShortcuts()
        {
            if (Input.GetKeyDown(KeyCode.F7)) showFps = !showFps;
            if (Player == null || Hud.AnyPanelOpen) return;
            if (Input.GetKeyDown(KeyCode.F1))
            {
                foreach (var t in new[] { ItemType.Madeira, ItemType.Graveto, ItemType.Folha, ItemType.Corda, ItemType.Pedra, ItemType.Carvao, ItemType.Cobre, ItemType.Ferro })
                    Inventory.Add(t, 15, false);
                Crafting.UnlockAll();
                Hud.Toast("Teste: materiais e todas as receitas", Hud.Gold);
            }
            if (Input.GetKeyDown(KeyCode.F2))
            {
                GiveBoat();
                if (Player.Driving == null) Boat.MoveTo(StartIsland.BoatSpawn, StartIsland.BoatSpawnRot);
                Hud.Toast("Teste: barco no fim do píer", Hud.Gold);
            }
            if (Input.GetKeyDown(KeyCode.F3) && StartIsland.Reefs != null && StartIsland.Reefs.Count > 0)
            {
                GiveBoat();
                if (Player.Driving != null) Player.StopDriving();
                Vector3 reef = StartIsland.Reefs[0].transform.position;
                Vector3 dir = (StartIsland.transform.position - reef); dir.y = 0; dir.Normalize();
                Vector3 pos = reef + dir * 14f + Vector3.up * 0.3f;
                Boat.MoveTo(pos, Quaternion.LookRotation(-dir));
                Player.BoardBoat(Boat);
                Hud.Toast("Teste: no recife da boia. Aperte R para ancorar.", Hud.Gold);
            }
            if (Input.GetKeyDown(KeyCode.F4))
            {
                Environment.TimeOfDay = Mathf.Repeat(Environment.TimeOfDay + 0.125f, 1f);
                Hud.Toast("Teste: hora " + Environment.ClockText, Hud.Gold);
            }
            if (Input.GetKeyDown(KeyCode.F5))
            {
                Inventory.Add(ItemType.Cobre, 10); Inventory.Add(ItemType.Carvao, 6); Inventory.Add(ItemType.Ferro, 4);
                Inventory.Add(ItemType.Perola, 2); Inventory.Add(ItemType.Concha, 5); Inventory.Add(ItemType.FragmentoMapa, 1);
            }
            if (Input.GetKeyDown(KeyCode.F6)) { Player.Heal(); Hud.Toast("Teste: vida e fôlego cheios", Hud.Gold); }
        }

        void Update()
        {
            float dt = Time.deltaTime;
            TestShortcuts();
            bool under = Environment.CameraUnderwater;
            lowPass.cutoffFrequency = Mathf.Lerp(lowPass.cutoffFrequency, under ? 650f : 22000f, dt * 8f);
            // Perto da praia as ondas são mais altas; longe, quase silêncio.
            Vector3 p = Player.transform.position;
            float shore = Mathf.Clamp01(1f - (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(StartIsland.transform.position.x, StartIsland.transform.position.z)) - StartIsland.Radius) / 160f);
            oceanAudio.volume = Mathf.Lerp(oceanAudio.volume, under ? 0.15f : 0.15f + shore * 0.35f, dt * 2f);
            underAudio.volume = Mathf.Lerp(underAudio.volume, under ? 0.45f : 0f, dt * 4f);

            // Gaivotas de dia, perto da ilha.
            gullTimer -= dt;
            if (gullTimer <= 0)
            {
                gullTimer = Random.Range(12f, 30f);
                bool day = Environment.TimeOfDay > 0.27f && Environment.TimeOfDay < 0.75f;
                if (day && !under && shore > 0.2f)
                    Sfx.Play(Sfx.Gull, p + new Vector3(Random.Range(-25f, 25f), 15f, Random.Range(-25f, 25f)), 0.35f, 0.12f);
            }

            // Música: toca uma faixa, pausa um pouco, toca outra.
            music.volume = MusicVolume;
            if (!music.isPlaying)
            {
                musicGap -= dt;
                if (musicGap <= 0)
                {
                    music.clip = Sfx.Clip(Sfx.Music[musicIndex % Sfx.Music.Length]);
                    musicIndex++;
                    if (music.clip != null) music.Play();
                    musicGap = Random.Range(40f, 90f);
                }
            }
        }

        // ---------- Economia ----------

        public int TakeAllCoins()
        {
            int n = Coins;
            Coins = 0;
            return n;
        }

        public void AddCoins(int n)
        {
            Coins += n;
            if (n > 0) Sfx.UI(Sfx.Coin, 0.6f);
        }

        public bool Spend(int n)
        {
            if (Coins < n) return false;
            Coins -= n;
            Sfx.UI(Sfx.Coin, 0.5f);
            return true;
        }

        public int Sell(ItemType t, int amount)
        {
            int have = Inventory.Count(t);
            amount = Mathf.Min(amount, have);
            int price = Items.Get(t).SellPrice;
            if (amount <= 0 || price <= 0) return 0;
            Inventory.Remove(t, amount);
            AddCoins(amount * price);
            return amount * price;
        }

        // ---------- Loja ----------

        public class Offer
        {
            public string Name;
            public string Description;
            public int Price;
            public System.Func<bool> Available;
            public System.Func<string> Unavailable; // motivo, se não disponível
            public System.Action Buy;
        }

        Offer[] offers;

        public Offer[] Offers
        {
            get
            {
                if (offers != null) return offers;
                offers = new[]
                {
                    new Offer
                    {
                        Name = "Bote a vapor", Price = 150,
                        Description = "Seu primeiro barco. Leva você para o mar aberto e até os minérios do fundo.",
                        Available = () => Boat == null,
                        Unavailable = () => "Você já tem um barco",
                        Buy = () =>
                        {
                            GiveBoat();
                            Hud.Toast("Seu barco está no fim do píer!", Hud.Good);
                        },
                    },
                    new Offer
                    {
                        Name = "Motor a vapor nível 2", Price = 120,
                        Description = "Caldeira maior: o barco fica 45% mais rápido.",
                        Available = () => Boat != null && Boat.MotorLevel == 1,
                        Unavailable = () => Boat == null ? "Compre um barco primeiro" : "Já instalado",
                        Buy = () => { Boat.MotorLevel = 2; Hud.Toast("Motor nível 2 instalado!", Hud.Good); },
                    },
                    new Offer
                    {
                        Name = "Motor a vapor nível 3", Price = 280,
                        Description = "Caldeira de latão reforçada: quase o dobro da velocidade.",
                        Available = () => Boat != null && Boat.MotorLevel == 2,
                        Unavailable = () => Boat == null ? "Compre um barco primeiro" : Boat.MotorLevel < 2 ? "Precisa do nível 2" : "Já instalado",
                        Buy = () => { Boat.MotorLevel = 3; Hud.Toast("Motor nível 3 instalado!", Hud.Good); },
                    },
                    new Offer
                    {
                        Name = "Corda longa da âncora", Price = 70,
                        Description = "A âncora alcança 36 m de fundo (a comum só 16 m).",
                        Available = () => Boat != null && Boat.RopeLength < 30f,
                        Unavailable = () => Boat == null ? "Compre um barco primeiro" : "Já comprada",
                        Buy = () => { Boat.RopeLength = 36f; Hud.Toast("Corda nova na âncora: 36 m!", Hud.Good); },
                    },
                    new Offer
                    {
                        Name = "Mochila maior", Price = 60,
                        Description = "+6 espaços na mochila.",
                        Available = () => BackpackLevel < 2,
                        Unavailable = () => "Já é a maior mochila daqui",
                        Buy = () =>
                        {
                            BackpackLevel++;
                            Inventory.Resize(Inventory.Size + 6);
                            Hud.Toast("Mochila maior! Agora tem " + Inventory.Size + " espaços.", Hud.Good);
                        },
                    },
                    new Offer
                    {
                        Name = "Tanque de oxigênio", Price = 90,
                        Description = "Fôlego 60% maior debaixo d'água.",
                        Available = () => OxygenLevel < 1,
                        Unavailable = () => "Já comprado",
                        Buy = () =>
                        {
                            OxygenLevel++;
                            Player.OxygenSeconds *= 1.6f;
                            Hud.Toast("Agora você aguenta mais tempo debaixo d'água!", Hud.Good);
                        },
                    },
                    new Offer
                    {
                        Name = "Rebocar barco até o píer", Price = 10,
                        Description = "Perdeu o barco? O mercador traz de volta.",
                        Available = () => Boat != null && !Boat.Driven,
                        Unavailable = () => Boat == null ? "Você não tem barco" : "Saia do barco primeiro",
                        Buy = () =>
                        {
                            Boat.MoveTo(StartIsland.BoatSpawn, StartIsland.BoatSpawnRot);
                            Hud.Toast("Barco de volta ao píer.", Hud.Good);
                        },
                    },
                    new Offer
                    {
                        Name = "Tocha", Price = 8,
                        Description = "Ilumina o caminho à noite. Segure na mão ou largue no chão.",
                        Available = () => Inventory.SpaceFor(ItemType.Tocha) > 0,
                        Unavailable = () => "Mochila cheia",
                        Buy = () => Inventory.Add(ItemType.Tocha, 1),
                    },
                    new Offer
                    {
                        Name = "Picareta de Pedra", Price = 15,
                        Description = "Uma picareta reserva, se você perder a sua.",
                        Available = () => Inventory.SpaceFor(ItemType.PicaretaPedra) > 0,
                        Unavailable = () => "Mochila cheia",
                        Buy = () => Inventory.Add(ItemType.PicaretaPedra, 1),
                    },
                };
                return offers;
            }
        }

        public Boat GiveBoat()
        {
            if (Boat == null) Boat = Boat.Spawn(StartIsland.BoatSpawn, StartIsland.BoatSpawnRot);
            return Boat;
        }

        public void SetCoinsForPreview(int c) => Coins = c;

        public bool TryBuy(Offer o)
        {
            if (!o.Available()) return false;
            if (!Spend(o.Price))
            {
                Hud.Toast("Moedas insuficientes.", Hud.Bad);
                return false;
            }
            o.Buy();
            return true;
        }
    }
}
