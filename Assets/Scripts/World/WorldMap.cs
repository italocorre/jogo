using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    public enum IslandType { Praia, Floresta, Montanha, Rochosa, Vulcanica }

    /// <summary>
    /// O mundo: 30 ilhas espalhadas no oceano. Só a ilha inicial é gerada no começo;
    /// as outras aparecem como silhueta de longe e são geradas aos poucos quando o jogador chega perto.
    /// </summary>
    public class WorldMap : MonoBehaviour
    {
        public const int IslandCount = 30;
        public const float WorldRadius = 9000f;
        public const float GenerateDistance = 1100f;
        public const float HideDistance = 2600f;

        public readonly List<Island> Islands = new List<Island>();
        public Island StartIsland { get; private set; }
        bool busy;

        static readonly string[] names =
        {
            "Ilha do Náufrago", "Ilha das Gaivotas", "Ilha do Coral", "Ilha da Tartaruga", "Ilha do Farol Caído", "Ilha das Palmas",
            "Ilha do Corvo", "Ilha da Bruma", "Ilha do Tubarão", "Ilha da Concha", "Ilha das Pedras Negras", "Ilha do Vulcão",
            "Ilha da Caveira", "Ilha do Vento", "Ilha da Lua", "Ilha do Sol Poente", "Ilha das Ostras", "Ilha do Ferro",
            "Ilha do Cobre", "Ilha da Neblina", "Ilha do Eco", "Ilha das Raias", "Ilha da Âncora", "Ilha do Polvo",
            "Ilha do Trovão", "Ilha Esmeralda", "Ilha da Fumaça", "Ilha das Cinzas", "Ilha do Último Porto", "Ilha Perdida",
        };

        public static string TypeName(IslandType t)
        {
            switch (t)
            {
                case IslandType.Praia: return "praia";
                case IslandType.Floresta: return "floresta";
                case IslandType.Montanha: return "montanha";
                case IslandType.Rochosa: return "rochosa";
                default: return "vulcânica";
            }
        }

        /// <summary>Cria as 30 ilhas. A inicial é escolhida ao acaso entre as mais próximas do centro.</summary>
        public void Build(int seed, int spawnSeed, bool preview)
        {
            Layers.Setup();
            var rng = new System.Random(seed);
            var positions = new List<Vector2>();
            // Uma no centro; as demais espalhadas com distância mínima entre elas.
            positions.Add(Vector2.zero);
            int guard = 0;
            while (positions.Count < IslandCount && guard++ < 20000)
            {
                float a = (float)rng.NextDouble() * Mathf.PI * 2f;
                float r = Mathf.Sqrt((float)rng.NextDouble()) * WorldRadius;
                var p = new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                bool ok = true;
                foreach (var q in positions) if ((q - p).sqrMagnitude < 1500f * 1500f) { ok = false; break; }
                if (ok) positions.Add(p);
            }

            for (int i = 0; i < positions.Count; i++)
            {
                var pos = positions[i];
                float dist = pos.magnitude;
                int tier = dist < 3500f ? 1 : dist < 6500f ? 2 : 3;
                IslandType type;
                double roll = rng.NextDouble();
                if (i == 0) type = IslandType.Montanha;
                else if (tier == 1) type = roll < 0.3 ? IslandType.Praia : roll < 0.6 ? IslandType.Floresta : roll < 0.85 ? IslandType.Montanha : IslandType.Rochosa;
                else if (tier == 2) type = roll < 0.2 ? IslandType.Praia : roll < 0.4 ? IslandType.Floresta : roll < 0.65 ? IslandType.Montanha : roll < 0.9 ? IslandType.Rochosa : IslandType.Vulcanica;
                else type = roll < 0.15 ? IslandType.Floresta : roll < 0.4 ? IslandType.Montanha : roll < 0.7 ? IslandType.Rochosa : IslandType.Vulcanica;

                var go = new GameObject(names[i % names.Length]);
                go.transform.SetParent(transform, false);
                go.transform.position = new Vector3(pos.x, 0, pos.y);
                var isl = go.AddComponent<Island>();
                isl.IslandName = names[i % names.Length];
                isl.Index = i;
                isl.Seed = seed * 131 + i * 977 + 11;
                isl.Type = type;
                isl.Tier = tier;
                isl.Radius = i == 0 ? 190f : Mathf.Lerp(70f, 200f, (float)rng.NextDouble()) * (type == IslandType.Vulcanica ? 0.8f : 1f);
                Islands.Add(isl);
            }

            // Ilha inicial: o mundo é igual para todos; muda só onde cada um nasce
            // (uma ilha do anel de dentro, grande o bastante e com árvores).
            var options = new List<Island>();
            foreach (var isl in Islands)
                if (isl.transform.position.magnitude < 3500f && isl.Radius >= 120f && (isl.Type == IslandType.Praia || isl.Type == IslandType.Floresta || isl.Type == IslandType.Montanha))
                    options.Add(isl);
            StartIsland = preview || options.Count == 0 ? Islands[0] : options[new System.Random(spawnSeed).Next(options.Count)];
            StartIsland.IsStart = true;

            BuildSeabed();
            foreach (var isl in Islands) if (!isl.IsStart) isl.BuildSilhouette();
            StartIsland.Discovered = true;
            StartIsland.Generate();
        }

        /// <summary>Fundo do mar do mundo todo (abaixo do fundo das ilhas).</summary>
        void BuildSeabed()
        {
            var mb = new MeshBuilder();
            float s = WorldRadius + 4000f, y = -35f;
            mb.AddQuad(new Vector3(-s, y, -s), new Vector3(-s, y, s), new Vector3(s, y, s), new Vector3(s, y, -s), new Color(0.40f, 0.46f, 0.43f));
            var go = new GameObject("FundoDoMar");
            go.transform.SetParent(transform, false);
            var mesh = mb.Build("FundoDoMar");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            go.AddComponent<MeshCollider>().sharedMesh = mesh;
        }

        /// <summary>Ilha mais próxima de um ponto (opcional: só as não descobertas).</summary>
        public Island Nearest(Vector3 p, bool undiscoveredOnly, Island except = null)
        {
            Island best = null; float bd = float.MaxValue;
            foreach (var isl in Islands)
            {
                if (isl == except || (undiscoveredOnly && isl.Discovered)) continue;
                float d = (isl.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = isl; }
            }
            return best;
        }

        /// <summary>Ilha não descoberta e ainda não revelada mais próxima.</summary>
        public Island NearestHidden(Vector3 p)
        {
            Island best = null; float bd = float.MaxValue;
            foreach (var isl in Islands)
            {
                if (isl.Discovered || isl.Revealed) continue;
                float d = (isl.transform.position - p).sqrMagnitude;
                if (d < bd) { bd = d; best = isl; }
            }
            return best;
        }

        public Island IslandAt(Vector3 p)
        {
            foreach (var isl in Islands)
            {
                Vector3 d = isl.transform.position - p; d.y = 0;
                if (d.magnitude < isl.Radius * 1.5f) return isl;
            }
            return null;
        }

        void Update()
        {
            var g = Game.Instance;
            if (g == null || g.Player == null) return;
            Vector3 pp = g.Player.transform.position;
            foreach (var isl in Islands)
            {
                Vector3 d = isl.transform.position - pp; d.y = 0;
                float dist = d.magnitude - isl.Radius;
                if (!isl.Discovered && dist < isl.Radius * 0.6f)
                {
                    isl.Discovered = true;
                    g.Hud.Toast("Ilha descoberta: " + isl.IslandName + " (" + TypeName(isl.Type) + ")", Hud.Gold);
                }
                if (!isl.Generated && !isl.Generating && !busy && dist < GenerateDistance)
                    StartCoroutine(GenerateOne(isl));
                if (isl.Generated) isl.SetDetailVisible(dist < HideDistance);
            }
        }

        System.Collections.IEnumerator GenerateOne(Island isl)
        {
            busy = true;
            yield return isl.GenerateRoutine();
            busy = false;
        }
    }
}
