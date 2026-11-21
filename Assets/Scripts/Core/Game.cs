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
        public Boat Boat { get; private set; }
        public int Coins { get; private set; }
        public int BackpackLevel { get; private set; }
        public int OxygenLevel { get; private set; }

        AudioSource oceanAudio;
        AudioLowPassFilter lowPass;

        void Awake()
        {
            Instance = this;
            Application.targetFrameRate = 144;
            Inventory = new Inventory(Inventory.HotbarSize + 12);

            foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) Destroy(c.gameObject);
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) Destroy(l.gameObject);

            StartIsland = WorldBuilder.BuildIsland();

            var playerGo = new GameObject("Jogador");
            playerGo.AddComponent<CharacterController>();
            Player = playerGo.AddComponent<PlayerController>();
            Player.Init(StartIsland.SpawnPoint, StartIsland.SpawnLook);
            Hands = playerGo.AddComponent<Hands>();
            Hands.Init(Player);

            Environment = gameObject.AddComponent<WorldEnvironment>();
            Environment.Init(Player.Cam);
            WorldBuilder.BuildOcean(Player.Cam.transform);

            oceanAudio = Player.Cam.gameObject.AddComponent<AudioSource>();
            oceanAudio.clip = Sfx.OceanLoop;
            oceanAudio.loop = true;
            oceanAudio.volume = 0.35f;
            oceanAudio.spatialBlend = 0f;
            oceanAudio.Play();
            lowPass = Player.Cam.gameObject.AddComponent<AudioLowPassFilter>();
            lowPass.cutoffFrequency = 22000f;

            // Começa só com a picareta.
            Inventory.Add(ItemType.PicaretaPedra, 1, false);
            Inventory.Selected = 0;

            Hud = gameObject.AddComponent<Hud>();
            Hud.Init(this);
        }

        void Start()
        {
            Icons.RenderAll();
            Hud.Toast("Bem-vindo à Ilha do Porto!", new Color(1f, 0.9f, 0.6f));
            Hud.Toast("Fale com o mercador na barraca listrada.", new Color(0.9f, 0.95f, 1f));
        }

        void Update()
        {
            bool under = Environment.CameraUnderwater;
            lowPass.cutoffFrequency = Mathf.Lerp(lowPass.cutoffFrequency, under ? 700f : 22000f, Time.deltaTime * 8f);
            oceanAudio.volume = Mathf.Lerp(oceanAudio.volume, under ? 0.55f : 0.35f, Time.deltaTime * 4f);
        }

        // ---------- Economia ----------

        public void AddCoins(int n)
        {
            Coins += n;
            if (n > 0) Sfx.Play(Sfx.Coin, Player.transform.position, 0.5f);
        }

        public bool Spend(int n)
        {
            if (Coins < n) return false;
            Coins -= n;
            Sfx.Play(Sfx.Coin, Player.transform.position, 0.4f);
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
                        Name = "Bote a motor", Price = 150,
                        Description = "Seu primeiro barco. Leva você para o mar aberto e até os minérios do fundo.",
                        Available = () => Boat == null,
                        Unavailable = () => "Você já tem um barco",
                        Buy = () =>
                        {
                            Boat = Boat.Spawn(StartIsland.BoatSpawn, StartIsland.BoatSpawnRot);
                            Hud.Toast("Seu barco está no fim do píer!", new Color(0.7f, 1f, 0.7f));
                        },
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
                            Hud.Toast("Mochila maior! Agora tem " + Inventory.Size + " espaços.", new Color(0.7f, 1f, 0.7f));
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
                            Hud.Toast("Agora você aguenta mais tempo debaixo d'água!", new Color(0.7f, 1f, 0.7f));
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
                            Hud.Toast("Barco de volta ao píer.", new Color(0.7f, 1f, 0.7f));
                        },
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

        public bool TryBuy(Offer o)
        {
            if (!o.Available()) return false;
            if (!Spend(o.Price))
            {
                Hud.Toast("Moedas insuficientes.", new Color(1f, 0.6f, 0.5f));
                return false;
            }
            o.Buy();
            return true;
        }
    }
}
