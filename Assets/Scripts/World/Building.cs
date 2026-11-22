using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace MarAberto
{
    public enum PieceKind { Piso, Parede, Pilar, Escada, Telhado, Bau, Cama, Fundicao, Bancada, Leme, Motor, Ancora, Antena, Bandeira, MudaCoqueiro, MudaArvore, Refinaria, MotorGas, Helicoptero, Canhao, Fogueira, Purificador, ColetorChuva, Porta, Janela, Cerca, Lampiao, Rede, Canteiro, PlantaBatata, PlantaMelancia }
    public enum Station { Nenhuma, Bancada, Fundicao, Refinaria, Fogueira }

    /// <summary>Uma peça construída (piso, parede, baú...). Fica na camada Build.</summary>
    public class BuildPiece : MonoBehaviour
    {
        public PieceKind Kind;
        public Recipe Recipe;
        public string Owner = "";
        public int Id;   // número da peça na partida (multiplayer/jogo salvo)
        public static readonly List<BuildPiece> All = new List<BuildPiece>();

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        public bool IsFloorLike => Kind == PieceKind.Piso || Kind == PieceKind.Telhado;
    }

    /// <summary>Modelos das peças de construção (feitos uma vez e copiados).</summary>
    public static class Building
    {
        public const float Cell = 2f;          // lado de um piso
        public const float FloorH = 0.2f;      // espessura do piso
        public const float WallH = 2.4f;       // altura da parede

        static readonly Dictionary<PieceKind, GameObject> templates = new Dictionary<PieceKind, GameObject>();
        static Transform templateRoot;
        static Color W(float k) => new Color(k, k, k, 1f);
        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        public static string Name(PieceKind k)
        {
            switch (k)
            {
                case PieceKind.Piso: return "Piso de madeira";
                case PieceKind.Parede: return "Parede de madeira";
                case PieceKind.Pilar: return "Pilar";
                case PieceKind.Escada: return "Escada";
                case PieceKind.Telhado: return "Telhado de folhas";
                case PieceKind.Bau: return "Baú";
                case PieceKind.Cama: return "Cama";
                case PieceKind.Fundicao: return "Fundição";
                case PieceKind.Leme: return "Leme";
                case PieceKind.Motor: return "Motor a vapor";
                case PieceKind.Ancora: return "Âncora";
                case PieceKind.Antena: return "Antena receptora";
                case PieceKind.Bandeira: return "Bandeira de território";
                case PieceKind.MudaCoqueiro: return "Muda de coqueiro";
                case PieceKind.MudaArvore: return "Muda de árvore";
                case PieceKind.Refinaria: return "Refinaria de gasolina";
                case PieceKind.MotorGas: return "Motor a gasolina";
                case PieceKind.Helicoptero: return "Helicóptero";
                case PieceKind.Canhao: return "Canhão";
                case PieceKind.Fogueira: return "Fogueira";
                case PieceKind.Purificador: return "Purificador de água";
                case PieceKind.ColetorChuva: return "Coletor de chuva";
                case PieceKind.Porta: return "Parede com porta";
                case PieceKind.Janela: return "Parede com janela";
                case PieceKind.Cerca: return "Cerca";
                case PieceKind.Lampiao: return "Lampião";
                case PieceKind.Rede: return "Rede coletora";
                case PieceKind.Canteiro: return "Canteiro de horta";
                case PieceKind.PlantaBatata: return "Pé de batata";
                case PieceKind.PlantaMelancia: return "Pé de melancia";
                default: return "Bancada";
            }
        }

        public static bool IsFurniture(PieceKind k) => k == PieceKind.Bau || k == PieceKind.Cama || k == PieceKind.Fundicao || k == PieceKind.Bancada
            || k == PieceKind.Leme || k == PieceKind.Motor || k == PieceKind.Ancora || k == PieceKind.Antena || k == PieceKind.Bandeira
            || k == PieceKind.MudaCoqueiro || k == PieceKind.MudaArvore || k == PieceKind.Refinaria || k == PieceKind.MotorGas || k == PieceKind.Helicoptero || k == PieceKind.Canhao || k == PieceKind.Fogueira || k == PieceKind.Purificador || k == PieceKind.ColetorChuva || k == PieceKind.Lampiao || k == PieceKind.Canteiro || k == PieceKind.PlantaBatata || k == PieceKind.PlantaMelancia;

        /// <summary>Peças que vão na borda do piso, como a parede.</summary>
        public static bool IsWallLike(PieceKind k) => k == PieceKind.Parede || k == PieceKind.Porta || k == PieceKind.Janela || k == PieceKind.Cerca || k == PieceKind.Rede;
        /// <summary>Paredes inteiras (aguentam telhado e outra parede em cima).</summary>
        public static bool IsFullWall(PieceKind k) => k == PieceKind.Parede || k == PieceKind.Porta || k == PieceKind.Janela;

        /// <summary>Tamanho da caixa usada para checar se cabe (no espaço da peça; pé em y = 0).</summary>
        public static Vector3 Size(PieceKind k)
        {
            switch (k)
            {
                case PieceKind.Piso: return new Vector3(Cell, FloorH, Cell);
                case PieceKind.Telhado: return new Vector3(Cell, FloorH, Cell);
                case PieceKind.Parede: return new Vector3(Cell, WallH, 0.16f);
                case PieceKind.Pilar: return new Vector3(0.26f, WallH, 0.26f);
                case PieceKind.Escada: return new Vector3(1.6f, WallH, Cell);
                case PieceKind.Bau: return new Vector3(0.9f, 0.65f, 0.6f);
                case PieceKind.Cama: return new Vector3(1.1f, 0.6f, 2.1f);
                case PieceKind.Fundicao: return new Vector3(1.4f, 1.9f, 1.4f);
                case PieceKind.Leme: return new Vector3(0.9f, 1.4f, 0.4f);
                case PieceKind.Motor: return new Vector3(0.9f, 1.3f, 0.9f);
                case PieceKind.Ancora: return new Vector3(1.0f, 0.8f, 0.7f);
                case PieceKind.Antena: return new Vector3(0.6f, 2.8f, 0.6f);
                case PieceKind.Bandeira: return new Vector3(0.5f, 4.2f, 0.5f);
                case PieceKind.MudaCoqueiro:
                case PieceKind.MudaArvore: return new Vector3(0.5f, 0.5f, 0.5f);
                case PieceKind.Refinaria: return new Vector3(1.6f, 2.4f, 1.2f);
                case PieceKind.MotorGas: return new Vector3(0.9f, 1.1f, 0.9f);
                case PieceKind.Helicoptero: return new Vector3(2.4f, 2.6f, 7.5f);
                case PieceKind.Canhao: return new Vector3(1.0f, 1.0f, 1.4f);
                case PieceKind.Fogueira: return new Vector3(1.0f, 0.6f, 1.0f);
                case PieceKind.Purificador: return new Vector3(0.9f, 1.3f, 0.9f);
                case PieceKind.ColetorChuva: return new Vector3(1.3f, 1.4f, 1.3f);
                case PieceKind.Porta:
                case PieceKind.Janela: return new Vector3(Cell, WallH, 0.16f);
                case PieceKind.Cerca: return new Vector3(Cell, 1.0f, 0.12f);
                case PieceKind.Lampiao: return new Vector3(0.4f, 1.7f, 0.4f);
                case PieceKind.Rede: return new Vector3(Cell, 0.9f, 0.12f);
                case PieceKind.Canteiro: return new Vector3(1.9f, 0.4f, 1.0f);
                case PieceKind.PlantaBatata:
                case PieceKind.PlantaMelancia: return new Vector3(0.5f, 0.4f, 0.5f);
                default: return new Vector3(1.7f, 1.0f, 0.85f);
            }
        }

        /// <summary>Cria a peça no mundo (pé em pos).</summary>
        public static GameObject Create(PieceKind k, Vector3 pos, Quaternion rot, Transform parent)
        {
            var tpl = Template(k);
            var go = Object.Instantiate(tpl, pos, rot, parent);
            go.name = Name(k);
            go.SetActive(true);
            return go;
        }

        /// <summary>Cópia só visual (sem colisão nem comportamento) para a prévia.</summary>
        public static GameObject CreateGhost(PieceKind k, Material ghost)
        {
            var go = Object.Instantiate(Template(k));
            go.SetActive(true);
            go.name = "Previa_" + k;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
            foreach (var b in go.GetComponentsInChildren<MonoBehaviour>(true)) Object.Destroy(b);
            foreach (var l in go.GetComponentsInChildren<Light>(true)) Object.Destroy(l);
            foreach (var a in go.GetComponentsInChildren<AudioSource>(true)) Object.Destroy(a);
            foreach (var lr in go.GetComponentsInChildren<LineRenderer>(true)) Object.Destroy(lr);
            foreach (var rb in go.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; Object.Destroy(rb); }
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                var mats = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = ghost;
                r.sharedMaterials = mats;
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.IgnoreRaycast;
            return go;
        }

        static GameObject Template(PieceKind k)
        {
            if (templates.TryGetValue(k, out var t) && t != null) return t;
            if (templateRoot == null)
            {
                templateRoot = new GameObject("ModelosDeConstrucao").transform;
                templateRoot.gameObject.SetActive(false);
            }
            t = Build(k);
            templates[k] = t;
            return t;
        }

        static void Part(GameObject root, string name, MeshBuilder mb, Material mat, bool shadows = true)
        {
            if (mb.VertexCount == 0) return;
            var g = new GameObject(name);
            g.transform.SetParent(root.transform, false);
            g.AddComponent<MeshFilter>().sharedMesh = mb.Build(name);
            var mr = g.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
        }

        static void Box(GameObject root, Vector3 center, Vector3 size)
        {
            var b = root.AddComponent<BoxCollider>();
            b.center = center;
            b.size = size;
        }

        static GameObject Build(PieceKind k)
        {
            var root = new GameObject(Name(k));
            root.transform.SetParent(templateRoot, false); // fica inativo: só serve de molde
            root.layer = Layers.Build;
            var planks = new MeshBuilder();
            var poles = new MeshBuilder();
            var other = new MeshBuilder();
            float h = Cell * 0.5f;
            switch (k)
            {
                case PieceKind.Piso:
                {
                    for (int i = 0; i < 6; i++)
                    {
                        float x = -h + (i + 0.5f) * Cell / 6f;
                        planks.AddBoxUV(new Vector3(x, FloorH - 0.04f, 0), new Vector3(Cell / 6f - 0.015f, 0.07f, Cell - 0.01f),
                            Quaternion.Euler(0, (Noise.Hash(i, 3) - 0.5f) * 0.8f, 0), W(0.82f + Noise.Hash(i, 9) * 0.3f), 0.5f);
                    }
                    foreach (float z in new[] { -h + 0.15f, h - 0.15f })
                        planks.AddBoxUV(new Vector3(0, 0.07f, z), new Vector3(Cell, 0.13f, 0.14f), Quaternion.identity, W(0.7f), 0.5f);
                    Box(root, new Vector3(0, FloorH * 0.5f, 0), new Vector3(Cell, FloorH, Cell));
                    break;
                }
                case PieceKind.Telhado:
                {
                    // Estrutura de ripas + folhas de coqueiro por cima (palha).
                    for (int i = 0; i < 4; i++)
                        planks.AddBoxUV(new Vector3(-h + 0.25f + i * 0.5f, 0.05f, 0), new Vector3(0.1f, 0.08f, Cell), Quaternion.identity, W(0.75f), 0.5f);
                    // Palha: camadas de feixes cor de palha seca, levemente sobrepostas (fecha o telhado).
                    for (int row = 0; row < 5; row++)
                    {
                        float z = -h + 0.2f + row * 0.4f;
                        planks.AddBoxUV(new Vector3(0, 0.13f + (row % 2) * 0.015f, z), new Vector3(Cell + 0.1f, 0.06f, 0.48f),
                            Quaternion.Euler(-4f, (Noise.Hash(row, 7) - 0.5f) * 2f, 0), new Color(1.0f, 0.86f, 0.55f, 1f), 0.6f);
                    }
                    Box(root, new Vector3(0, FloorH * 0.5f, 0), new Vector3(Cell, FloorH, Cell));
                    break;
                }
                case PieceKind.Parede:
                {
                    for (int i = 0; i < 6; i++)
                    {
                        float x = -h + (i + 0.5f) * Cell / 6f;
                        planks.AddBoxUV(new Vector3(x, WallH * 0.5f, 0), new Vector3(Cell / 6f - 0.02f, WallH - 0.02f, 0.07f),
                            Quaternion.identity, W(0.8f + Noise.Hash(i, 5) * 0.3f), 0.5f);
                    }
                    foreach (float y in new[] { 0.35f, WallH - 0.35f })
                        planks.AddBoxUV(new Vector3(0, y, -0.06f), new Vector3(Cell, 0.14f, 0.06f), Quaternion.identity, W(0.68f), 0.5f);
                    Box(root, new Vector3(0, WallH * 0.5f, 0), new Vector3(Cell, WallH, 0.16f));
                    break;
                }
                case PieceKind.Pilar:
                    poles.AddTube(new[] { Vector3.zero, Vector3.up * WallH }, new[] { 0.12f, 0.11f }, 10, (i, t) => Color.white, null, true, true, null, 0.6f, 1f);
                    Box(root, new Vector3(0, WallH * 0.5f, 0), new Vector3(0.26f, WallH, 0.26f));
                    break;
                case PieceKind.Escada:
                {
                    int steps = 8;
                    float run = Cell / steps, rise = WallH / steps;
                    for (int i = 0; i < steps; i++)
                    {
                        Vector3 c = new Vector3(0, rise * (i + 1) - 0.04f, -h + run * (i + 0.5f));
                        planks.AddBoxUV(c, new Vector3(1.5f, 0.07f, run + 0.04f), Quaternion.identity, W(0.8f + Noise.Hash(i, 2) * 0.25f), 0.5f);
                        var b = root.AddComponent<BoxCollider>();
                        b.center = new Vector3(0, rise * (i + 1) * 0.5f, c.z);
                        b.size = new Vector3(1.5f, rise * (i + 1), run);
                    }
                    foreach (float x in new[] { -0.78f, 0.78f })
                        poles.AddTube(new[] { new Vector3(x, 0, -h), new Vector3(x, WallH, h) }, new[] { 0.06f, 0.06f }, 8, (i, t) => Color.white, null, true, true, null, 0.6f, 1f);
                    break;
                }
                case PieceKind.Bau:
                {
                    var model = ModelLibrary.Spawn("treasure_chest", Vector3.zero, Quaternion.identity, 0.6f, ModelLibrary.Fit.Height, -1, ModelLibrary.Col.None, root.transform);
                    if (model == null) planks.AddBoxUV(new Vector3(0, 0.3f, 0), new Vector3(0.9f, 0.6f, 0.6f), Quaternion.identity, W(0.8f), 0.5f);
                    Box(root, new Vector3(0, 0.32f, 0), new Vector3(0.9f, 0.64f, 0.6f));
                    root.AddComponent<Chest>();
                    break;
                }
                case PieceKind.Cama:
                {
                    float L = 2.0f, Wd = 1.0f;
                    planks.AddBoxUV(new Vector3(0, 0.25f, 0), new Vector3(Wd, 0.1f, L), Quaternion.identity, W(0.8f), 0.5f);
                    foreach (var p in new[] { new Vector3(-0.45f, 0, -0.95f), new Vector3(0.45f, 0, -0.95f), new Vector3(-0.45f, 0, 0.95f), new Vector3(0.45f, 0, 0.95f) })
                        poles.AddTube(new[] { p, p + Vector3.up * (p.z > 0 ? 0.7f : 0.45f) }, new[] { 0.05f, 0.05f }, 8, (i, t) => Color.white, null, true, true, null, 0.6f, 1f);
                    planks.AddBoxUV(new Vector3(0, 0.55f, 0.98f), new Vector3(Wd, 0.3f, 0.05f), Quaternion.identity, W(0.7f), 0.5f); // cabeceira
                    other.AddBox(new Vector3(0, 0.36f, -0.05f), new Vector3(Wd * 0.92f, 0.16f, L * 0.9f), Quaternion.identity, C(0.78f, 0.72f, 0.56f)); // colchão de palha
                    other.AddSphere(new Vector3(0, 0.47f, 0.72f), new Vector3(0.3f, 0.07f, 0.15f), C(0.88f, 0.86f, 0.80f), 2); // travesseiro
                    other.AddBox(new Vector3(0, 0.455f, -0.35f), new Vector3(Wd * 0.96f, 0.04f, L * 0.56f), Quaternion.identity, C(0.45f, 0.20f, 0.18f)); // cobertor
                    Box(root, new Vector3(0, 0.3f, 0), new Vector3(1.1f, 0.6f, 2.1f));
                    root.AddComponent<Bed>();
                    break;
                }
                case PieceKind.Fundicao:
                {
                    // Forno de pedra com boca acesa e chaminé.
                    var rng = new System.Random(5);
                    for (int i = 0; i < 26; i++)
                    {
                        float a = i * 0.9f, y = 0.15f + (i / 9) * 0.42f, r = 0.62f - (i / 9) * 0.12f;
                        Vector3 p = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                        if (p.z > 0.35f && y < 0.7f && Mathf.Abs(p.x) < 0.3f) continue; // boca do forno
                        other.AddRock(p, 0.22f, rng.Next(), n => Color.Lerp(C(0.40f, 0.39f, 0.37f), C(0.58f, 0.56f, 0.52f), n.y * 0.5f + 0.5f), 0.25f);
                    }
                    other.AddCylinder(new Vector3(0, 1.1f, 0), new Vector3(0, 1.9f, 0), 0.22f, 0.17f, 10, C(0.42f, 0.41f, 0.39f));
                    other.AddSphere(new Vector3(0, 0.3f, 0.3f), new Vector3(0.22f, 0.18f, 0.12f), C(1f, 0.45f, 0.1f), 2); // brasa
                    var lg = new GameObject("Brasa");
                    lg.transform.SetParent(root.transform, false);
                    lg.transform.localPosition = new Vector3(0, 0.35f, 0.55f);
                    var l = lg.AddComponent<Light>();
                    l.type = LightType.Point; l.color = new Color(1f, 0.5f, 0.2f); l.range = 4f; l.intensity = 1.2f; l.shadows = LightShadows.None;
                    Box(root, new Vector3(0, 0.6f, 0), new Vector3(1.4f, 1.2f, 1.4f));
                    root.AddComponent<CraftStation>().Kind = Station.Fundicao;
                    break;
                }
                case PieceKind.Leme:
                {
                    // Coluna com timão (roda de madeira) virado para quem pilota.
                    poles.AddTube(new[] { Vector3.zero, new Vector3(0, 1.0f, 0) }, new[] { 0.09f, 0.08f }, 10, (i, t) => Color.white, null, true, true, null, 0.6f, 1f);
                    Vector3 hub = new Vector3(0, 1.05f, -0.12f);
                    int seg = 20; float R = 0.42f;
                    for (int i = 0; i < seg; i++)
                    {
                        float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                        poles.AddTube(new[] { hub + new Vector3(Mathf.Cos(a0) * R, Mathf.Sin(a0) * R, 0), hub + new Vector3(Mathf.Cos(a1) * R, Mathf.Sin(a1) * R, 0) },
                            new[] { 0.035f, 0.035f }, 6, (j, t) => Color.white, null, false, false, null, 0.6f, 1f);
                    }
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        Vector3 d = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                        poles.AddTube(new[] { hub, hub + d * (R + 0.14f) }, new[] { 0.025f, 0.02f }, 6, (j, t) => Color.white, null, false, true, null, 0.6f, 1f);
                    }
                    other.AddCylinder(hub + Vector3.forward * 0.05f, hub - Vector3.forward * 0.06f, 0.07f, 0.07f, 12, C(0.85f, 0.65f, 0.3f)); // eixo de latão
                    var seat = new GameObject("Assento");
                    seat.transform.SetParent(root.transform, false);
                    seat.transform.localPosition = new Vector3(0, 0, -0.85f);
                    Box(root, new Vector3(0, 0.7f, 0), new Vector3(0.9f, 1.4f, 0.4f));
                    root.AddComponent<RaftHelm>();
                    break;
                }
                case PieceKind.Fogueira:
                {
                    // Roda de pedras realista (Poly Haven), lenha cruzada e grelha.
                    var fp = ModelLibrary.Spawn("stone_fire_pit", Vector3.zero, Quaternion.identity, 1.1f, ModelLibrary.Fit.Length, -1, ModelLibrary.Col.None, root.transform);
                    if (fp == null)
                    {
                        var rng = new System.Random(8);
                        for (int i = 0; i < 9; i++)
                        {
                            float a = i * Mathf.PI * 2f / 9f;
                            other.AddRock(new Vector3(Mathf.Cos(a) * 0.42f, 0.07f, Mathf.Sin(a) * 0.42f), 0.1f, rng.Next(), _ => C(0.45f, 0.44f, 0.42f), 0.2f);
                        }
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        float a = i * Mathf.PI / 2f + 0.3f;
                        Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        poles.AddTube(new[] { d * 0.3f + Vector3.up * 0.06f, Vector3.up * 0.28f - d * 0.02f }, new[] { 0.045f, 0.035f }, 7, (j, t) => Color.white, null, true, true, null, 0.6f, 1f);
                    }
                    foreach (float x in new[] { -0.45f, 0.45f })
                        poles.AddTube(new[] { new Vector3(x, 0, 0), new Vector3(x, 0.6f, 0) }, new[] { 0.025f, 0.02f }, 6, (j, t) => Color.white, null, true, true, null, 0.6f, 1f);
                    other.AddCylinder(new Vector3(-0.47f, 0.6f, 0), new Vector3(0.47f, 0.6f, 0), 0.012f, 0.012f, 6, C(0.3f, 0.3f, 0.32f));
                    Box(root, new Vector3(0, 0.15f, 0), new Vector3(1.0f, 0.3f, 1.0f));
                    root.AddComponent<CraftStation>().Kind = Station.Fogueira;
                    root.AddComponent<CampfireFx>();
                    break;
                }
                case PieceKind.Purificador:
                {
                    // Panela de metal sobre fogo, tubo de cobre e jarro coletando a água limpa.
                    Color metal = C(0.55f, 0.56f, 0.58f), copper = C(0.85f, 0.5f, 0.28f), glass = C(0.65f, 0.85f, 0.95f);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * Mathf.PI / 3f;
                        other.AddRock(new Vector3(Mathf.Cos(a) * 0.3f, 0.08f, Mathf.Sin(a) * 0.3f), 0.1f, i + 3, _ => C(0.45f, 0.44f, 0.42f), 0.2f);
                    }
                    other.AddCylinder(new Vector3(0, 0.25f, 0), new Vector3(0, 0.6f, 0), 0.3f, 0.32f, 16, metal);
                    other.AddSphere(new Vector3(0, 0.62f, 0), new Vector3(0.3f, 0.12f, 0.3f), metal, 2);
                    other.AddCylinder(new Vector3(0, 0.72f, 0), new Vector3(0.35f, 1.15f, 0), 0.03f, 0.03f, 8, copper);
                    other.AddCylinder(new Vector3(0.35f, 1.15f, 0), new Vector3(0.55f, 0.55f, 0), 0.03f, 0.03f, 8, copper);
                    other.AddCylinder(new Vector3(0.55f, 0.05f, 0), new Vector3(0.55f, 0.45f, 0), 0.12f, 0.1f, 12, glass);
                    Box(root, new Vector3(0, 0.5f, 0), new Vector3(0.9f, 1.0f, 0.7f));
                    root.AddComponent<WaterPurifier>();
                    break;
                }
                case PieceKind.Porta:
                {
                    // Parede com vão de porta (1 m) no meio e uma folha que abre (E).
                    const float dw = 1.0f, dh = 2.05f;
                    float side = (Cell - dw) * 0.5f;
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        for (int i = 0; i < 2; i++)
                        {
                            float x = sx * (dw * 0.5f + side * (i + 0.5f) / 2f);
                            planks.AddBoxUV(new Vector3(x, WallH * 0.5f, 0), new Vector3(side / 2f - 0.02f, WallH - 0.02f, 0.07f), Quaternion.identity, W(0.8f + Noise.Hash(i, 5 + (int)sx) * 0.3f), 0.5f);
                        }
                        Box(root, new Vector3(sx * (dw * 0.5f + side * 0.5f), WallH * 0.5f, 0), new Vector3(side, WallH, 0.16f));
                        poles.AddTube(new[] { new Vector3(sx * dw * 0.5f, 0, -0.02f), new Vector3(sx * dw * 0.5f, dh, -0.02f) }, new[] { 0.05f, 0.05f }, 8, (i, t) => Color.white, null, true, true, null, 0.6f, 1f);
                    }
                    planks.AddBoxUV(new Vector3(0, (dh + WallH) * 0.5f, 0), new Vector3(dw, WallH - dh, 0.07f), Quaternion.identity, W(0.78f), 0.5f);
                    planks.AddBoxUV(new Vector3(0, dh + 0.04f, -0.06f), new Vector3(dw + 0.2f, 0.1f, 0.06f), Quaternion.identity, W(0.65f), 0.5f);
                    Box(root, new Vector3(0, (dh + WallH) * 0.5f, 0), new Vector3(dw, WallH - dh, 0.16f));
                    // Folha (dobradiça no lado esquerdo).
                    var leaf = new GameObject("Folha");
                    leaf.transform.SetParent(root.transform, false);
                    leaf.transform.localPosition = new Vector3(-dw * 0.5f + 0.04f, 0, 0);
                    var lm = new MeshBuilder();
                    for (int i = 0; i < 4; i++)
                        lm.AddBoxUV(new Vector3(0.02f + (i + 0.5f) * (dw - 0.1f) / 4f, dh * 0.5f, 0), new Vector3((dw - 0.1f) / 4f - 0.015f, dh - 0.06f, 0.05f), Quaternion.identity, W(0.75f + Noise.Hash(i, 11) * 0.25f), 0.5f);
                    foreach (float y in new[] { 0.4f, dh - 0.4f })
                        lm.AddBoxUV(new Vector3((dw - 0.1f) * 0.5f, y, 0.04f), new Vector3(dw - 0.14f, 0.12f, 0.04f), Quaternion.identity, W(0.62f), 0.5f);
                    leaf.AddComponent<MeshFilter>().sharedMesh = lm.Build("Folha");
                    leaf.AddComponent<MeshRenderer>().sharedMaterial = Mats.Planks;
                    var knob = new MeshBuilder();
                    knob.AddSphere(new Vector3(dw - 0.2f, 1.0f, 0.07f), Vector3.one * 0.03f, C(0.3f, 0.28f, 0.26f), 1);
                    knob.AddSphere(new Vector3(dw - 0.2f, 1.0f, -0.07f), Vector3.one * 0.03f, C(0.3f, 0.28f, 0.26f), 1);
                    var kg = new GameObject("Macaneta");
                    kg.transform.SetParent(leaf.transform, false);
                    kg.AddComponent<MeshFilter>().sharedMesh = knob.Build("Macaneta");
                    kg.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
                    var lc = leaf.AddComponent<BoxCollider>();
                    lc.center = new Vector3((dw - 0.1f) * 0.5f, dh * 0.5f, 0);
                    lc.size = new Vector3(dw - 0.1f, dh - 0.04f, 0.07f);
                    root.AddComponent<DoorPiece>().Leaf = leaf.transform;
                    break;
                }
                case PieceKind.Janela:
                {
                    // Parede com janela (1,1 x 0,8 m) e cruz de madeira.
                    const float ww = 1.1f, wy0 = 1.0f, wy1 = 1.8f;
                    float side = (Cell - ww) * 0.5f;
                    foreach (float sx in new[] { -1f, 1f })
                    {
                        float cx = sx * (ww * 0.5f + side * 0.5f);
                        planks.AddBoxUV(new Vector3(cx, WallH * 0.5f, 0), new Vector3(side - 0.02f, WallH - 0.02f, 0.07f), Quaternion.identity, W(0.8f + Noise.Hash(3, 5 + (int)sx) * 0.3f), 0.5f);
                        Box(root, new Vector3(cx, WallH * 0.5f, 0), new Vector3(side, WallH, 0.16f));
                    }
                    planks.AddBoxUV(new Vector3(0, wy0 * 0.5f, 0), new Vector3(ww, wy0, 0.07f), Quaternion.identity, W(0.82f), 0.5f);
                    planks.AddBoxUV(new Vector3(0, (wy1 + WallH) * 0.5f, 0), new Vector3(ww, WallH - wy1, 0.07f), Quaternion.identity, W(0.78f), 0.5f);
                    Box(root, new Vector3(0, wy0 * 0.5f, 0), new Vector3(ww, wy0, 0.16f));
                    Box(root, new Vector3(0, (wy1 + WallH) * 0.5f, 0), new Vector3(ww, WallH - wy1, 0.16f));
                    planks.AddBoxUV(new Vector3(0, wy0 - 0.02f, -0.07f), new Vector3(ww + 0.16f, 0.07f, 0.16f), Quaternion.identity, W(0.62f), 0.5f); // peitoril
                    poles.AddTube(new[] { new Vector3(0, wy0, 0), new Vector3(0, wy1, 0) }, new[] { 0.025f, 0.025f }, 6, (i, t) => Color.white, null, true, true, null, 0.6f, 1f);
                    poles.AddTube(new[] { new Vector3(-ww * 0.5f, (wy0 + wy1) * 0.5f, 0), new Vector3(ww * 0.5f, (wy0 + wy1) * 0.5f, 0) }, new[] { 0.025f, 0.025f }, 6, (i, t) => Color.white, null, true, true, null, 0.6f, 1f);
                    break;
                }
                case PieceKind.Cerca:
                {
                    // Guarda-corpo: dois postes, corrimão e travessas.
                    foreach (float x in new[] { -h + 0.06f, h - 0.06f })
                        poles.AddTube(new[] { new Vector3(x, 0, 0), new Vector3(x, 1.0f, 0) }, new[] { 0.06f, 0.055f }, 8, (i, t) => Color.white, null, true, true, null, 0.6f, 1f);
                    planks.AddBoxUV(new Vector3(0, 0.98f, 0), new Vector3(Cell, 0.07f, 0.12f), Quaternion.identity, W(0.75f), 0.5f);
                    planks.AddBoxUV(new Vector3(0, 0.5f, 0), new Vector3(Cell - 0.1f, 0.06f, 0.05f), Quaternion.identity, W(0.7f), 0.5f);
                    for (int i = 0; i < 7; i++)
                    {
                        float x = -h + 0.3f + i * (Cell - 0.6f) / 6f;
                        planks.AddBoxUV(new Vector3(x, 0.5f, 0.02f), new Vector3(0.05f, 0.92f, 0.03f), Quaternion.Euler(0, 0, (Noise.Hash(i, 4) - 0.5f) * 3f), W(0.8f), 0.5f);
                    }
                    Box(root, new Vector3(0, 0.6f, 0), new Vector3(Cell, 1.2f, 0.12f));
                    break;
                }
                case PieceKind.Lampiao:
                {
                    // Poste de madeira com lampião de ferro e vidro (acende sozinho).
                    poles.AddTube(new[] { Vector3.zero, Vector3.up * 1.45f }, new[] { 0.06f, 0.05f }, 8, (i, t) => Color.white, null, true, true, null, 0.6f, 1f);
                    planks.AddBoxUV(new Vector3(0, 0.05f, 0), new Vector3(0.36f, 0.1f, 0.36f), Quaternion.identity, W(0.7f), 0.5f);
                    Color iron = C(0.18f, 0.18f, 0.19f), glass = C(1f, 0.85f, 0.55f);
                    other.AddCylinder(new Vector3(0, 1.45f, 0), new Vector3(0, 1.48f, 0), 0.12f, 0.12f, 8, iron);
                    other.AddCylinder(new Vector3(0, 1.48f, 0), new Vector3(0, 1.7f, 0), 0.09f, 0.1f, 8, glass);
                    other.AddCylinder(new Vector3(0, 1.7f, 0), new Vector3(0, 1.8f, 0), 0.12f, 0.03f, 8, iron);
                    for (int i = 0; i < 4; i++)
                    {
                        var q = Quaternion.Euler(0, i * 90f + 45f, 0);
                        other.AddCylinder(q * new Vector3(0.1f, 1.48f, 0), q * new Vector3(0.1f, 1.7f, 0), 0.008f, 0.008f, 4, iron);
                    }
                    other.AddCylinder(new Vector3(0, 1.8f, 0), new Vector3(0, 1.86f, 0), 0.025f, 0.025f, 6, iron);
                    Box(root, new Vector3(0, 0.85f, 0), new Vector3(0.3f, 1.7f, 0.3f));
                    root.AddComponent<LanternFx>();
                    break;
                }
                case PieceKind.Rede:
                {
                    // Rede pendurada para fora da borda da jangada: pega destroços que passam.
                    foreach (float x in new[] { -h + 0.08f, h - 0.08f })
                        poles.AddTube(new[] { new Vector3(x, 0.8f, 0), new Vector3(x, -0.7f, 0.9f) }, new[] { 0.05f, 0.045f }, 8, (i, t) => Color.white, null, true, true, null, 0.6f, 1f);
                    poles.AddTube(new[] { new Vector3(-h, 0.8f, 0), new Vector3(h, 0.8f, 0) }, new[] { 0.045f, 0.045f }, 8, (i, t) => Color.white, null, true, true, null, 0.6f, 1f);
                    Color rope = C(0.72f, 0.64f, 0.45f);
                    // malha: cordas cruzadas entre o alto e a ponta dentro da água
                    for (int i = 0; i <= 8; i++)
                    {
                        float x = -h + 0.1f + i * (Cell - 0.2f) / 8f;
                        other.AddCylinder(new Vector3(x, 0.8f, 0), new Vector3(x, -0.7f, 0.9f), 0.008f, 0.008f, 4, rope, false);
                    }
                    for (int j = 0; j <= 6; j++)
                    {
                        float u = j / 6f;
                        Vector3 a = Vector3.Lerp(new Vector3(-h + 0.1f, 0.8f, 0), new Vector3(-h + 0.1f, -0.7f, 0.9f), u);
                        other.AddCylinder(a, a + Vector3.right * (Cell - 0.2f), 0.008f, 0.008f, 4, rope, false);
                    }
                    Box(root, new Vector3(0, 0.45f, 0), new Vector3(Cell, 0.9f, 0.12f));
                    root.AddComponent<NetCatcher>();
                    break;
                }
                case PieceKind.ColetorChuva:
                {
                    // Funil de lona sobre quatro estacas, escorrendo para um balde.
                    foreach (var p in new[] { new Vector3(-0.55f, 0, -0.55f), new Vector3(0.55f, 0, -0.55f), new Vector3(-0.55f, 0, 0.55f), new Vector3(0.55f, 0, 0.55f) })
                        poles.AddTube(new[] { p, p + Vector3.up * 1.3f }, new[] { 0.04f, 0.035f }, 7, (j, t) => Color.white, null, true, true, null, 0.6f, 1f);
                    Color cloth = C(0.78f, 0.72f, 0.58f);
                    Vector3 c0 = new Vector3(0, 0.75f, 0);
                    Vector3[] cs = { new Vector3(-0.62f, 1.3f, -0.62f), new Vector3(0.62f, 1.3f, -0.62f), new Vector3(0.62f, 1.3f, 0.62f), new Vector3(-0.62f, 1.3f, 0.62f) };
                    for (int i = 0; i < 4; i++) { other.AddTri(c0, cs[i], cs[(i + 1) % 4], cloth); other.AddTri(c0, cs[(i + 1) % 4], cs[i], cloth * 0.85f); }
                    other.AddCylinder(new Vector3(0, 0, 0), new Vector3(0, 0.35f, 0), 0.22f, 0.25f, 14, C(0.45f, 0.32f, 0.2f));
                    other.AddCylinder(new Vector3(0, 0.3f, 0), new Vector3(0, 0.31f, 0), 0.23f, 0.23f, 14, C(0.4f, 0.6f, 0.75f));
                    Box(root, new Vector3(0, 0.7f, 0), new Vector3(1.3f, 1.4f, 1.3f));
                    var wp = root.AddComponent<WaterPurifier>();
                    wp.RainCollector = true;
                    wp.Seconds = 60f;
                    break;
                }
                case PieceKind.Canhao:
                {
                    // Canhão realista (Poly Haven); se faltar, um simples.
                    var cm = ModelLibrary.Spawn("cannon_01", Vector3.zero, Quaternion.identity, 1.5f, ModelLibrary.Fit.Length, -1, ModelLibrary.Col.None, root.transform);
                    if (cm == null)
                    {
                        planks.AddBoxUV(new Vector3(0, 0.3f, 0), new Vector3(0.7f, 0.2f, 1.1f), Quaternion.identity, W(0.75f), 0.5f);
                        other.AddCylinder(new Vector3(0, 0.55f, -0.45f), new Vector3(0, 0.55f, 0.9f), 0.17f, 0.12f, 14, C(0.16f, 0.16f, 0.17f));
                    }
                    Box(root, new Vector3(0, 0.45f, 0.1f), new Vector3(0.9f, 0.9f, 1.4f));
                    root.AddComponent<CannonPiece>();
                    break;
                }
                case PieceKind.Helicoptero:
                {
                    // Cabine arredondada, vidro, cauda, esquis, hélice principal e de cauda, e metralhadora (aparece quando instalada).
                    Color body = C(0.22f, 0.34f, 0.24f), dark = C(0.15f, 0.15f, 0.17f), glass = C(0.55f, 0.75f, 0.85f), stripe = C(0.85f, 0.7f, 0.3f);
                    other.AddSphere(new Vector3(0, 1.25f, 0.4f), new Vector3(0.95f, 0.85f, 1.7f), body, 3);
                    other.AddSphere(new Vector3(0, 1.45f, 1.55f), new Vector3(0.85f, 0.7f, 0.75f), glass, 3);
                    other.AddCylinder(new Vector3(0, 1.45f, -1.1f), new Vector3(0, 1.75f, -4.6f), 0.36f, 0.14f, 12, body);
                    other.AddCylinder(new Vector3(0, 1.48f, -1.5f), new Vector3(0, 1.62f, -3.0f), 0.38f, 0.3f, 12, stripe, false);
                    other.AddBox(new Vector3(0, 2.15f, -4.5f), new Vector3(0.08f, 0.9f, 0.55f), Quaternion.Euler(-15, 0, 0), body);
                    other.AddBox(new Vector3(0, 1.8f, -4.3f), new Vector3(1.2f, 0.06f, 0.35f), Quaternion.identity, body);
                    foreach (float x in new[] { -0.85f, 0.85f })
                    {
                        other.AddCylinder(new Vector3(x, 0.1f, -1.3f), new Vector3(x, 0.1f, 1.6f), 0.05f, 0.05f, 8, dark);
                        other.AddCylinder(new Vector3(x, 0.1f, 1.6f), new Vector3(x, 0.25f, 1.95f), 0.05f, 0.05f, 8, dark);
                        other.AddCylinder(new Vector3(x, 0.1f, -0.6f), new Vector3(x * 0.7f, 0.75f, -0.5f), 0.04f, 0.04f, 6, dark);
                        other.AddCylinder(new Vector3(x, 0.1f, 0.9f), new Vector3(x * 0.7f, 0.75f, 0.9f), 0.04f, 0.04f, 6, dark);
                    }
                    other.AddCylinder(new Vector3(0, 2.05f, 0.3f), new Vector3(0, 2.4f, 0.3f), 0.12f, 0.1f, 10, dark); // mastro do rotor
                    var rotor = new GameObject("Rotor");
                    rotor.transform.SetParent(root.transform, false);
                    rotor.transform.localPosition = new Vector3(0, 2.42f, 0.3f);
                    var rm = new MeshBuilder();
                    for (int i = 0; i < 4; i++)
                    {
                        Quaternion q = Quaternion.Euler(0, i * 90f, 0);
                        rm.AddBox(q * new Vector3(0, 0, 2.3f), new Vector3(0.28f, 0.04f, 4.4f), q, dark);
                    }
                    rm.AddCylinder(new Vector3(0, -0.06f, 0), new Vector3(0, 0.08f, 0), 0.18f, 0.18f, 12, dark);
                    rotor.AddComponent<MeshFilter>().sharedMesh = rm.Build("Rotor");
                    rotor.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
                    var tail = new GameObject("RotorCauda");
                    tail.transform.SetParent(root.transform, false);
                    tail.transform.localPosition = new Vector3(0.12f, 2.1f, -4.6f);
                    var tm = new MeshBuilder();
                    tm.AddBox(Vector3.zero, new Vector3(0.04f, 1.1f, 0.12f), Quaternion.identity, dark);
                    tm.AddBox(Vector3.zero, new Vector3(0.04f, 0.12f, 1.1f), Quaternion.identity, dark);
                    tail.AddComponent<MeshFilter>().sharedMesh = tm.Build("RotorCauda");
                    tail.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
                    var gunGo = new GameObject("Metralhadora");
                    gunGo.transform.SetParent(root.transform, false);
                    gunGo.transform.localPosition = new Vector3(0, 0.7f, 1.6f);
                    var gm = new MeshBuilder();
                    gm.AddBox(new Vector3(0, 0, 0), new Vector3(0.22f, 0.22f, 0.45f), Quaternion.identity, dark);
                    gm.AddCylinder(new Vector3(0, 0.02f, 0.2f), new Vector3(0, 0.02f, 1.05f), 0.05f, 0.045f, 8, dark);
                    gunGo.AddComponent<MeshFilter>().sharedMesh = gm.Build("Metralhadora");
                    gunGo.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
                    var tip = new GameObject("Ponta");
                    tip.transform.SetParent(gunGo.transform, false);
                    tip.transform.localPosition = new Vector3(0, 0.02f, 1.1f);
                    gunGo.SetActive(false);
                    var seatH = new GameObject("Assento");
                    seatH.transform.SetParent(root.transform, false);
                    seatH.transform.localPosition = new Vector3(0, 0.55f, 0.9f);
                    Box(root, new Vector3(0, 1.25f, 0.3f), new Vector3(1.9f, 1.7f, 3.6f));
                    Box(root, new Vector3(0, 1.6f, -2.9f), new Vector3(0.6f, 0.6f, 3.4f));
                    Box(root, new Vector3(0, 0.1f, 0.15f), new Vector3(1.9f, 0.2f, 3.2f));
                    root.AddComponent<Helicopter>();
                    break;
                }
                case PieceKind.Canteiro:
                {
                    // Caixa de tábuas com terra escura (planta-se em cima, até na jangada).
                    foreach (float z in new[] { -0.47f, 0.47f })
                        planks.AddBoxUV(new Vector3(0, 0.18f, z), new Vector3(1.9f, 0.36f, 0.06f), Quaternion.identity, W(0.72f), 0.5f);
                    foreach (float x in new[] { -0.92f, 0.92f })
                        planks.AddBoxUV(new Vector3(x, 0.18f, 0), new Vector3(0.06f, 0.36f, 1.0f), Quaternion.identity, W(0.68f), 0.5f);
                    for (int i = 0; i < 6; i++)
                        other.AddRock(new Vector3(-0.7f + i * 0.28f, 0.27f, 0), 0.22f, 80 + i, _ => C(0.24f, 0.17f, 0.11f), 0.2f, new Vector3(0.75f, 0.25f, 2f), 1);
                    Box(root, new Vector3(0, 0.17f, 0), new Vector3(1.9f, 0.34f, 1.0f));
                    break;
                }
                case PieceKind.PlantaBatata:
                case PieceKind.PlantaMelancia:
                {
                    bool melon = k == PieceKind.PlantaMelancia;
                    other.AddRock(new Vector3(0, 0.0f, 0), 0.2f, 6, _ => C(0.3f, 0.21f, 0.14f), 0.15f, new Vector3(1f, 0.3f, 1f), 1);
                    var broto = new GameObject("Broto");
                    broto.transform.SetParent(root.transform, false);
                    var sb = new MeshBuilder();
                    Color leaf = melon ? C(0.3f, 0.55f, 0.2f) : C(0.35f, 0.5f, 0.22f);
                    int nl = melon ? 9 : 7;
                    for (int i = 0; i < nl; i++)
                    {
                        float a = i * Mathf.PI * 2f / nl + (i % 2) * 0.3f;
                        Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        Vector3 side = Vector3.Cross(Vector3.up, d) * (melon ? 0.07f : 0.05f);
                        Vector3 b0 = Vector3.up * (melon ? 0.05f : 0.12f + (i % 3) * 0.04f);
                        Vector3 tip = b0 + d * (melon ? 0.3f : 0.2f) + Vector3.up * (melon ? 0.02f : 0.1f);
                        Color c0 = leaf; c0.a = 0.3f; Color c1 = leaf; c1.a = 1f;
                        sb.AddTri(b0 - side, tip, b0 + side, c0, c1, c0, Vector3.up);
                        sb.AddTri(b0 + side, tip, b0 - side, c0, c1, c0, Vector3.up);
                    }
                    broto.AddComponent<MeshFilter>().sharedMesh = sb.Build("Broto");
                    broto.AddComponent<MeshRenderer>().sharedMaterial = Mats.Foliage;
                    var fruto = new GameObject("Fruto");
                    fruto.transform.SetParent(root.transform, false);
                    var fb = new MeshBuilder();
                    if (melon)
                    {
                        fb.AddRock(new Vector3(0.12f, 0.12f, 0.05f), 0.15f, 7, n => Mathf.Repeat(Mathf.Atan2(n.z, n.x) * 3f, 1f) > 0.5f ? C(0.18f, 0.4f, 0.15f) : C(0.3f, 0.55f, 0.22f), 0.03f, new Vector3(1.25f, 0.95f, 1f), 2);
                    }
                    else
                    {
                        for (int i = 0; i < 3; i++)
                            fb.AddRock(new Vector3(Mathf.Cos(i * 2.1f) * 0.12f, 0.04f, Mathf.Sin(i * 2.1f) * 0.12f), 0.05f, 8 + i, _ => C(0.62f, 0.47f, 0.3f), 0.15f, new Vector3(1.2f, 0.8f, 1f), 1);
                    }
                    fruto.AddComponent<MeshFilter>().sharedMesh = fb.Build("Fruto");
                    fruto.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
                    fruto.SetActive(false);
                    var bc = root.AddComponent<BoxCollider>();
                    bc.center = new Vector3(0, 0.2f, 0); bc.size = new Vector3(0.5f, 0.4f, 0.5f); bc.isTrigger = true;
                    root.AddComponent<Crop>().Melon = melon;
                    break;
                }
                case PieceKind.MudaCoqueiro:
                case PieceKind.MudaArvore:
                {
                    // Montinho de terra com broto (cresce sozinho).
                    other.AddRock(new Vector3(0, 0.02f, 0), 0.28f, 5, _ => C(0.33f, 0.24f, 0.16f), 0.15f, new Vector3(1f, 0.3f, 1f));
                    var broto = new GameObject("Broto");
                    broto.transform.SetParent(root.transform, false);
                    var sb = new MeshBuilder();
                    int nLeaves = k == PieceKind.MudaCoqueiro ? 5 : 7;
                    Color leaf = k == PieceKind.MudaCoqueiro ? C(0.35f, 0.58f, 0.2f) : C(0.25f, 0.5f, 0.18f);
                    sb.AddCylinder(Vector3.zero, Vector3.up * 0.25f, 0.02f, 0.012f, 6, C(0.45f, 0.35f, 0.2f));
                    for (int i = 0; i < nLeaves; i++)
                    {
                        float a = i * Mathf.PI * 2f / nLeaves;
                        Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                        Vector3 side = Vector3.Cross(Vector3.up, d) * 0.04f;
                        Vector3 b0 = Vector3.up * 0.22f, tip = b0 + d * 0.22f + Vector3.up * 0.05f;
                        Color c0 = leaf; c0.a = 0.3f; Color c1 = leaf; c1.a = 1f;
                        sb.AddTri(b0 - side, tip, b0 + side, c0, c1, c0, Vector3.up);
                        sb.AddTri(b0 + side, tip, b0 - side, c0, c1, c0, Vector3.up);
                    }
                    broto.AddComponent<MeshFilter>().sharedMesh = sb.Build("Broto");
                    broto.AddComponent<MeshRenderer>().sharedMaterial = Mats.Foliage;
                    Box(root, new Vector3(0, 0.2f, 0), new Vector3(0.5f, 0.4f, 0.5f));
                    root.AddComponent<Sapling>().Palm = k == PieceKind.MudaCoqueiro;
                    break;
                }
                case PieceKind.Refinaria:
                {
                    // Tanque de ferro, serpentina de cobre, fornalha e galões.
                    Color iron = C(0.25f, 0.26f, 0.28f), copper = C(0.85f, 0.5f, 0.28f), red = C(0.75f, 0.15f, 0.12f);
                    planks.AddBoxUV(new Vector3(0, 0.08f, 0), new Vector3(1.6f, 0.16f, 1.2f), Quaternion.identity, W(0.7f), 0.5f);
                    other.AddCylinder(new Vector3(-0.35f, 0.16f, 0), new Vector3(-0.35f, 1.5f, 0), 0.42f, 0.42f, 18, iron);
                    other.AddSphere(new Vector3(-0.35f, 1.5f, 0), new Vector3(0.42f, 0.2f, 0.42f), iron);
                    other.AddCylinder(new Vector3(-0.35f, 1.65f, 0), new Vector3(-0.35f, 2.4f, 0), 0.07f, 0.06f, 10, iron);
                    for (int i = 0; i < 6; i++)
                    {
                        float y = 0.4f + i * 0.16f;
                        other.AddCylinder(new Vector3(0.1f, y, -0.25f), new Vector3(0.6f, y + 0.08f, 0.25f), 0.03f, 0.03f, 6, copper);
                    }
                    other.AddCylinder(new Vector3(-0.35f, 1.3f, 0), new Vector3(0.35f, 1.4f, 0), 0.04f, 0.04f, 8, copper);
                    other.AddBox(new Vector3(0.55f, 0.36f, 0.3f), new Vector3(0.28f, 0.4f, 0.18f), Quaternion.identity, red);
                    other.AddBox(new Vector3(0.55f, 0.36f, -0.05f), new Vector3(0.28f, 0.4f, 0.18f), Quaternion.Euler(0, 8, 0), red);
                    other.AddSphere(new Vector3(-0.35f, 0.3f, 0.42f), new Vector3(0.16f, 0.1f, 0.04f), C(1f, 0.5f, 0.1f), 2);
                    Box(root, new Vector3(0, 1.0f, 0), new Vector3(1.6f, 2.0f, 1.2f));
                    root.AddComponent<CraftStation>().Kind = Station.Refinaria;
                    break;
                }
                case PieceKind.Motor:
                case PieceKind.MotorGas:
                {
                    // Caldeira de ferro (vapor) ou motor vermelho (gasolina) e hélice atrás.
                    bool gas = k == PieceKind.MotorGas;
                    Color iron = gas ? C(0.7f, 0.12f, 0.1f) : C(0.2f, 0.2f, 0.22f), brass = gas ? C(0.6f, 0.6f, 0.62f) : C(0.85f, 0.65f, 0.3f);
                    planks.AddBoxUV(new Vector3(0, 0.05f, 0), new Vector3(0.9f, 0.1f, 0.9f), Quaternion.identity, W(0.7f), 0.5f);
                    other.AddCylinder(new Vector3(0, 0.1f, 0), new Vector3(0, 0.75f, 0), 0.32f, 0.32f, 18, iron);
                    other.AddSphere(new Vector3(0, 0.75f, 0), new Vector3(0.32f, 0.14f, 0.32f), iron);
                    other.AddCylinder(new Vector3(0, 0.4f, 0), new Vector3(0, 0.46f, 0), 0.335f, 0.335f, 18, brass, false);
                    other.AddCylinder(new Vector3(0, 0.8f, 0), new Vector3(0, 1.25f, 0), 0.07f, 0.06f, 12, brass);
                    other.AddCylinder(new Vector3(0, 0.25f, -0.3f), new Vector3(0, 0.0f, -0.75f), 0.03f, 0.03f, 8, iron); // eixo
                    var helice = new GameObject("Helice");
                    helice.transform.SetParent(root.transform, false);
                    helice.transform.localPosition = new Vector3(0, -0.05f, -0.8f);
                    var pm = new MeshBuilder();
                    for (int i = 0; i < 3; i++)
                    {
                        Quaternion q = Quaternion.Euler(0, 0, i * 120f);
                        pm.AddBox(q * new Vector3(0, 0.12f, 0), new Vector3(0.08f, 0.22f, 0.02f), q * Quaternion.Euler(0, 25, 0), brass);
                    }
                    helice.AddComponent<MeshFilter>().sharedMesh = pm.Build("Helice");
                    helice.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
                    Box(root, new Vector3(0, 0.6f, 0), new Vector3(0.8f, 1.2f, 0.8f));
                    var rm = root.AddComponent<RaftMotor>();
                    if (gas) { rm.Power = 2.3f; rm.FuelItem = ItemType.Gasolina; rm.SecondsPerUnit = 180f; }
                    break;
                }
                case PieceKind.Ancora:
                {
                    // Guincho com corda enrolada e a âncora de ferro pendurada na borda.
                    Color iron = C(0.25f, 0.25f, 0.27f), rope = C(0.75f, 0.63f, 0.42f);
                    foreach (float x in new[] { -0.42f, 0.42f })
                        planks.AddBoxUV(new Vector3(x, 0.3f, 0), new Vector3(0.08f, 0.6f, 0.35f), Quaternion.identity, W(0.75f), 0.5f);
                    other.AddCylinder(new Vector3(-0.38f, 0.45f, 0), new Vector3(0.38f, 0.45f, 0), 0.13f, 0.13f, 14, rope);
                    other.AddCylinder(new Vector3(-0.46f, 0.45f, 0), new Vector3(-0.6f, 0.45f, 0), 0.03f, 0.03f, 8, iron);
                    other.AddCylinder(new Vector3(-0.6f, 0.45f, 0), new Vector3(-0.6f, 0.65f, 0), 0.025f, 0.025f, 8, iron); // manivela
                    // âncora
                    Vector3 top = new Vector3(0, 0.4f, 0.5f);
                    other.AddCylinder(top, top + Vector3.down * 0.55f, 0.035f, 0.035f, 8, iron);
                    other.AddCylinder(top + Vector3.down * 0.1f + Vector3.left * 0.15f, top + Vector3.down * 0.1f + Vector3.right * 0.15f, 0.025f, 0.025f, 8, iron);
                    Vector3 b = top + Vector3.down * 0.55f;
                    other.AddCylinder(b, b + new Vector3(-0.22f, 0.18f, 0), 0.03f, 0.02f, 8, iron);
                    other.AddCylinder(b, b + new Vector3(0.22f, 0.18f, 0), 0.03f, 0.02f, 8, iron);
                    Box(root, new Vector3(0, 0.4f, 0), new Vector3(1.0f, 0.8f, 0.5f));
                    root.AddComponent<RaftAnchorPiece>();
                    break;
                }
                case PieceKind.Bandeira:
                {
                    // Mastro alto com bandeira de pano que balança ao vento e pedras na base.
                    poles.AddTube(new[] { Vector3.zero, Vector3.up * 4.2f }, new[] { 0.08f, 0.05f }, 10, (i, t) => Color.white, null, true, true, null, 0.6f, 1f);
                    var rng = new System.Random(3);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 1.05f;
                        other.AddRock(new Vector3(Mathf.Cos(a) * 0.28f, 0.08f, Mathf.Sin(a) * 0.28f), 0.14f, rng.Next(), _ => C(0.5f, 0.49f, 0.46f), 0.2f);
                    }
                    var cloth = new MeshBuilder();
                    int cols = 8, rows = 4;
                    for (int x = 0; x < cols; x++)
                        for (int y = 0; y < rows; y++)
                        {
                            float u0 = x / (float)cols, u1 = (x + 1) / (float)cols, v0 = y / (float)rows, v1 = (y + 1) / (float)rows;
                            Vector3 P(float u, float v) => new Vector3(0.06f + u * 1.3f, 3.2f + v * 0.85f, Mathf.Sin(u * 5f) * 0.06f * u);
                            Color c = (x + y) % 2 == 0 ? C(0.75f, 0.15f, 0.12f) : C(0.12f, 0.10f, 0.10f);
                            Color A(float u) { var cc = c; cc.a = u; return cc; }
                            cloth.AddTri(P(u0, v0), P(u0, v1), P(u1, v1), A(u0), A(u0), A(u1), Vector3.forward);
                            cloth.AddTri(P(u0, v0), P(u1, v1), P(u1, v0), A(u0), A(u1), A(u1), Vector3.forward);
                        }
                    Part(root, "Pano", cloth, Mats.Foliage, true);
                    Box(root, new Vector3(0, 2.1f, 0), new Vector3(0.4f, 4.2f, 0.4f));
                    root.AddComponent<TerritoryFlag>();
                    break;
                }
                case PieceKind.Antena:
                {
                    // Mastro com fios de cobre em espiral e uma caixa de rádio na base.
                    Color copper = C(0.85f, 0.5f, 0.28f), dark = C(0.2f, 0.2f, 0.22f);
                    poles.AddTube(new[] { Vector3.zero, Vector3.up * 2.7f }, new[] { 0.07f, 0.04f }, 8, (i, t) => Color.white, null, true, true, null, 0.6f, 1f);
                    for (int i = 0; i < 3; i++)
                    {
                        float y = 1.6f + i * 0.35f, r = 0.32f - i * 0.07f;
                        int seg = 16;
                        for (int k2 = 0; k2 < seg; k2++)
                        {
                            float a0 = k2 * Mathf.PI * 2f / seg, a1 = (k2 + 1) * Mathf.PI * 2f / seg;
                            other.AddCylinder(new Vector3(Mathf.Cos(a0) * r, y, Mathf.Sin(a0) * r), new Vector3(Mathf.Cos(a1) * r, y, Mathf.Sin(a1) * r), 0.015f, 0.015f, 5, copper, false);
                        }
                    }
                    other.AddSphere(new Vector3(0, 2.75f, 0), Vector3.one * 0.07f, C(1f, 0.3f, 0.2f), 2);
                    planks.AddBoxUV(new Vector3(0, 0.22f, 0.15f), new Vector3(0.45f, 0.44f, 0.3f), Quaternion.identity, W(0.75f), 0.5f);
                    other.AddBox(new Vector3(0, 0.3f, 0.31f), new Vector3(0.3f, 0.14f, 0.02f), Quaternion.identity, dark);
                    Box(root, new Vector3(0, 1.4f, 0), new Vector3(0.5f, 2.8f, 0.5f));
                    root.AddComponent<ReceptorPiece>();
                    break;
                }
                case PieceKind.Bancada:
                {
                    planks.AddBoxUV(new Vector3(0, 0.92f, 0), new Vector3(1.7f, 0.08f, 0.8f), Quaternion.identity, W(0.85f), 0.5f);
                    planks.AddBoxUV(new Vector3(0, 0.35f, 0), new Vector3(1.5f, 0.05f, 0.65f), Quaternion.identity, W(0.7f), 0.5f);
                    foreach (var p in new[] { new Vector3(-0.75f, 0, -0.33f), new Vector3(0.75f, 0, -0.33f), new Vector3(-0.75f, 0, 0.33f), new Vector3(0.75f, 0, 0.33f) })
                        poles.AddTube(new[] { p, p + Vector3.up * 0.9f }, new[] { 0.05f, 0.05f }, 8, (i, t) => Color.white, null, true, true, null, 0.6f, 1f);
                    // ferramentas em cima
                    other.AddCylinder(new Vector3(-0.4f, 0.98f, 0.1f), new Vector3(0.0f, 0.98f, 0.18f), 0.018f, 0.018f, 6, C(0.55f, 0.38f, 0.22f));
                    other.AddBox(new Vector3(0.02f, 0.99f, 0.18f), new Vector3(0.06f, 0.06f, 0.14f), Quaternion.identity, C(0.35f, 0.35f, 0.37f));
                    other.AddRock(new Vector3(0.5f, 1.0f, -0.1f), 0.09f, 3, _ => C(0.5f, 0.49f, 0.47f), 0.2f);
                    Box(root, new Vector3(0, 0.5f, 0), new Vector3(1.7f, 1.0f, 0.85f));
                    root.AddComponent<CraftStation>().Kind = Station.Bancada;
                    break;
                }
            }
            Part(root, "Tabuas", planks, Mats.Planks);
            Part(root, "Estacas", poles, Mats.Poles);
            Part(root, "Outros", other, Mats.LowPoly);
            var bp = root.AddComponent<BuildPiece>();
            bp.Kind = k;
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Build;
            return root;
        }
    }
}
