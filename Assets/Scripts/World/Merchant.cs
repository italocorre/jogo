using UnityEngine;

namespace MarAberto
{
    /// <summary>O mercador da ilha: compra seus itens e vende o barco e melhorias.</summary>
    public class Merchant : MonoBehaviour, IInteractable
    {
        public string MerchantName = "Seu Joca";
        Transform body, head, armR;
        float t;

        public string Prompt => "Falar com " + MerchantName + " (mercador)";
        public bool CanInteract => true;

        static readonly string[] greetings =
        {
            "Opa! Tem minério pra vender?",
            "Bem-vindo à minha barraca, marujo!",
            "Cobre, carvão, conchas... eu compro de tudo!",
            "Com um barco você chega nos recifes, onde tem ferro e pérolas!",
            "Viu a boia vermelha no mar? Ali tem um recife. Solte a âncora e mergulhe!",
            "Dizem que há mais recifes escondidos, longe da costa...",
        };

        static Color C(float r, float g, float b) => new Color(r, g, b, 1f);

        public static Merchant Create(Transform parent, Vector3 pos, Vector3 lookAt)
        {
            var root = new GameObject("Mercador");
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            Vector3 f = lookAt - pos; f.y = 0;
            root.transform.rotation = Quaternion.LookRotation(f.sqrMagnitude > 0.01f ? f.normalized : Vector3.forward);

            BuildStall(root.transform);

            // Personagem atrás do balcão
            var npc = new GameObject("Joca");
            npc.transform.SetParent(root.transform, false);
            npc.transform.localPosition = new Vector3(0, 0, -0.9f);
            var m = npc.AddComponent<Merchant>();
            m.BuildCharacter();
            var col = npc.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.9f, 0);
            col.height = 1.8f;
            col.radius = 0.4f;
            // O balcão também abre a loja.
            var counter = root.transform.Find("Balcao");
            if (counter != null) counter.gameObject.AddComponent<MerchantProxy>().Target = m;
            return m;
        }

        static void BuildStall(Transform root)
        {
            var wood = C(0.60f, 0.42f, 0.25f);
            var woodDark = C(0.42f, 0.29f, 0.17f);
            var red = C(0.85f, 0.25f, 0.22f);
            var cream = C(0.97f, 0.92f, 0.80f);

            var mb = new MeshBuilder();     // pano do toldo (cor por vértice)
            var planks = new MeshBuilder(); // madeira com textura
            var poles = new MeshBuilder();
            Color W(float k) => new Color(k, k, k, 1f);
            // Chão de tábuas
            for (int i = 0; i < 13; i++)
                planks.AddBoxUV(new Vector3(-2.05f + i * 0.34f, 0.06f, -0.6f), new Vector3(0.32f, 0.08f, 3.4f), Quaternion.Euler(0, (Noise.Hash(i, 2) - 0.5f) * 1.5f, 0), W(0.85f + Noise.Hash(i, 7) * 0.25f), 0.5f);
            // Vigas sob o piso
            foreach (float z in new[] { -2.1f, -0.6f, 0.9f })
                poles.AddTube(new[] { new Vector3(-2.2f, -0.05f, z), new Vector3(2.2f, -0.05f, z) }, new[] { 0.08f, 0.08f }, 8, (i, t) => Color.white, null, true, true, null, 0.6f, 1f);
            // Postes
            foreach (var p in new[] { new Vector3(-2.1f, 0, 0.9f), new Vector3(2.1f, 0, 0.9f), new Vector3(-2.1f, 0, -2.1f), new Vector3(2.1f, 0, -2.1f) })
                poles.AddTube(new[] { p - Vector3.up * 0.4f, p + Vector3.up * (p.z > 0 ? 2.6f : 3.1f) }, new[] { 0.1f, 0.085f }, 10, (i, t) => Color.white, null, false, true, null, 0.6f, 1f);
            // Travessas do toldo
            poles.AddTube(new[] { new Vector3(-2.3f, 2.58f, 0.9f), new Vector3(2.3f, 2.58f, 0.9f) }, new[] { 0.06f, 0.06f }, 8, (i, t) => Color.white, null, true, true, null, 0.6f, 1f);
            poles.AddTube(new[] { new Vector3(-2.3f, 3.08f, -2.1f), new Vector3(2.3f, 3.08f, -2.1f) }, new[] { 0.06f, 0.06f }, 8, (i, t) => Color.white, null, true, true, null, 0.6f, 1f);
            // Toldo listrado de lona (inclinado para a frente, com leve barriga)
            int stripes = 8, rows = 6;
            for (int i = 0; i < stripes; i++)
            {
                float x0 = -2.4f + i * 4.8f / stripes, x1 = x0 + 4.8f / stripes;
                Color c = i % 2 == 0 ? C(0.70f, 0.20f, 0.17f) : C(0.86f, 0.80f, 0.68f);
                for (int r = 0; r < rows; r++)
                {
                    float t0 = r / (float)rows, t1 = (r + 1) / (float)rows;
                    Vector3 A(float x, float t) => new Vector3(x, Mathf.Lerp(3.15f, 2.6f, t) - Mathf.Sin(t * Mathf.PI) * 0.12f, Mathf.Lerp(-2.4f, 1.3f, t));
                    mb.AddQuad(A(x0, t0), A(x1, t0), A(x1, t1), A(x0, t1), c * 0.75f);
                    mb.AddQuad(A(x0, t1), A(x1, t1), A(x1, t0), A(x0, t0), c);
                }
                Vector3 fa = new Vector3(x0, 2.6f, 1.3f), fb = new Vector3(x1, 2.6f, 1.3f), fm = new Vector3((x0 + x1) / 2, 2.38f, 1.31f);
                mb.AddTri(fa, fb, fm, c);
                mb.AddTri(fb, fa, fm, c * 0.85f);
            }

            var go = new GameObject("Barraca");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mb.Build("Toldo");
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            var bcol = go.AddComponent<BoxCollider>();
            bcol.center = new Vector3(0, 0.05f, -0.6f);
            bcol.size = new Vector3(4.4f, 0.12f, 3.4f);
            var pl = new GameObject("Tabuas"); pl.transform.SetParent(go.transform, false);
            pl.AddComponent<MeshFilter>().sharedMesh = planks.Build("TabuasBarraca");
            pl.AddComponent<MeshRenderer>().sharedMaterial = Mats.Planks;
            var po = new GameObject("Postes"); po.transform.SetParent(go.transform, false);
            po.AddComponent<MeshFilter>().sharedMesh = poles.Build("PostesBarraca");
            po.AddComponent<MeshRenderer>().sharedMaterial = Mats.Poles;

            // Props realistas
            GameObject Prop(string id, Vector3 local, float yaw, float size, int part = -1)
                => ModelLibrary.Spawn(id, root.TransformPoint(local), root.rotation * Quaternion.Euler(0, yaw, 0), size, ModelLibrary.Fit.Height, part, ModelLibrary.Col.Box, go.transform);
            Prop("wooden_crate_01", new Vector3(-2.75f, 0f, 0.3f), 12f, 0.7f);
            Prop("wooden_crate_01", new Vector3(-2.7f, 0.7f, 0.25f), -20f, 0.5f);
            Prop("wooden_barrels_01", new Vector3(2.75f, 0f, 0.5f), 40f, 1.0f, 0);
            Prop("wooden_barrels_01", new Vector3(2.8f, 0f, -0.6f), 0f, 1.0f, 1);
            Prop("treasure_chest", new Vector3(-1.4f, 0.1f, -1.9f), 180f, 0.55f);
            var lantern = ModelLibrary.Spawn("wooden_lantern_01", root.TransformPoint(new Vector3(1.6f, 2.0f, 1.0f)), root.rotation, 0.4f, ModelLibrary.Fit.Height, -1, ModelLibrary.Col.None, go.transform);
            if (lantern != null)
            {
                var lgo = new GameObject("LuzLanterna");
                lgo.transform.SetParent(lantern.transform, false);
                lgo.transform.localPosition = Vector3.up * 0.18f;
                var light = lgo.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.72f, 0.4f);
                light.range = 6f;
                light.intensity = 1.2f;
                light.shadows = LightShadows.None;
            }

            // Balcão (separado, para ser clicável)
            var cmb = new MeshBuilder();
            for (int i = 0; i < 4; i++)
                cmb.AddBoxUV(new Vector3(0, 0.18f + i * 0.25f, 0.95f), new Vector3(3.6f, 0.24f, 0.05f), Quaternion.identity, W(0.8f + Noise.Hash(i, 4) * 0.25f), 0.5f);
            cmb.AddBoxUV(new Vector3(-1.78f, 0.55f, 0.6f), new Vector3(0.05f, 1.0f, 0.7f), Quaternion.identity, W(0.85f), 0.5f);
            cmb.AddBoxUV(new Vector3(1.78f, 0.55f, 0.6f), new Vector3(0.05f, 1.0f, 0.7f), Quaternion.identity, W(0.85f), 0.5f);
            for (int i = 0; i < 3; i++)
                cmb.AddBoxUV(new Vector3(0, 1.08f, 0.35f + i * 0.28f), new Vector3(3.8f, 0.07f, 0.27f), Quaternion.identity, W(0.75f + i * 0.1f), 0.5f);
            var counter = new GameObject("Balcao");
            counter.transform.SetParent(root, false);
            counter.AddComponent<MeshFilter>().sharedMesh = cmb.Build("Balcao");
            counter.AddComponent<MeshRenderer>().sharedMaterial = Mats.Planks;
            var cc2 = counter.AddComponent<BoxCollider>();
            cc2.center = new Vector3(0, 0.55f, 0.62f);
            cc2.size = new Vector3(3.8f, 1.1f, 0.85f);
            // Moedas no balcão
            var coins = new MeshBuilder();
            for (int i = 0; i < 6; i++)
                coins.AddCylinder(new Vector3(0.6f + i * 0.03f, 1.12f + i * 0.02f, 0.6f), new Vector3(0.6f + i * 0.03f, 1.135f + i * 0.02f, 0.6f), 0.07f, 0.07f, 14, C(1f, 0.8f, 0.3f));
            var cg = new GameObject("Moedas"); cg.transform.SetParent(counter.transform, false);
            cg.AddComponent<MeshFilter>().sharedMesh = coins.Build("Moedas");
            cg.AddComponent<MeshRenderer>().sharedMaterial = Mats.ForItem(ItemType.Cobre);
        }

        void BuildCharacter()
        {
            var skin = C(0.87f, 0.64f, 0.48f);
            var shirt = C(0.92f, 0.90f, 0.84f);
            var vest = C(0.55f, 0.16f, 0.14f);
            var pants = C(0.24f, 0.28f, 0.40f);
            var boots = C(0.22f, 0.14f, 0.09f);
            var hat = C(0.16f, 0.13f, 0.12f);
            var gold = C(0.95f, 0.76f, 0.30f);
            var beard = C(0.88f, 0.87f, 0.84f);

            Transform Part(string name, Transform parent, Vector3 pos)
            {
                var g = new GameObject(name);
                g.transform.SetParent(parent, false);
                g.transform.localPosition = pos;
                return g.transform;
            }
            void Mesh(Transform tr, MeshBuilder mb)
            {
                tr.gameObject.AddComponent<MeshFilter>().sharedMesh = mb.Build(tr.name);
                tr.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            }

            // Pernas e botas
            var legs = new MeshBuilder();
            foreach (float x in new[] { -0.12f, 0.12f })
            {
                legs.AddCapsule(new Vector3(x, 0.32f, 0), new Vector3(x, 0.8f, 0), 0.1f, pants);
                legs.AddCylinder(new Vector3(x, 0.0f, 0.02f), new Vector3(x, 0.36f, 0.0f), 0.12f, 0.11f, 14, boots);
                legs.AddSphere(new Vector3(x, 0.06f, 0.1f), new Vector3(0.11f, 0.07f, 0.15f), boots);
            }
            Mesh(Part("Pernas", transform, Vector3.zero), legs);

            body = Part("Corpo", transform, new Vector3(0, 0.84f, 0));
            var torso = new MeshBuilder();
            torso.AddSphere(new Vector3(0, 0.3f, 0), new Vector3(0.3f, 0.38f, 0.22f), shirt, 3); // barriga/camisa
            torso.AddSphere(new Vector3(0, 0.34f, -0.01f), new Vector3(0.31f, 0.34f, 0.2f), vest, 3);
            torso.AddCylinder(new Vector3(0, 0.02f, 0), new Vector3(0, 0.12f, 0), 0.29f, 0.3f, 18, C(0.35f, 0.22f, 0.12f)); // cinto
            torso.AddSphere(new Vector3(0, 0.07f, 0.29f), new Vector3(0.06f, 0.05f, 0.02f), gold); // fivela
            for (int i = 0; i < 3; i++) torso.AddSphere(new Vector3(0, 0.2f + i * 0.12f, 0.205f), Vector3.one * 0.02f, gold);
            // Braço esquerdo apoiado no balcão
            torso.AddCapsule(new Vector3(-0.32f, 0.55f, 0), new Vector3(-0.38f, 0.22f, 0.12f), 0.075f, shirt);
            torso.AddSphere(new Vector3(-0.38f, 0.16f, 0.18f), Vector3.one * 0.07f, skin);
            Mesh(body, torso);

            armR = Part("Braco", body, new Vector3(0.32f, 0.56f, 0));
            var arm = new MeshBuilder();
            arm.AddCapsule(Vector3.zero, new Vector3(0.04f, -0.36f, 0.02f), 0.075f, shirt);
            arm.AddSphere(new Vector3(0.05f, -0.45f, 0.03f), Vector3.one * 0.07f, skin);
            Mesh(armR, arm);

            head = Part("Cabeca", body, new Vector3(0, 0.7f, 0));
            var hm = new MeshBuilder();
            hm.AddCylinder(new Vector3(0, -0.06f, 0), new Vector3(0, 0.05f, 0), 0.08f, 0.08f, 12, skin, false); // pescoço
            hm.AddSphere(new Vector3(0, 0.17f, 0), new Vector3(0.17f, 0.19f, 0.17f), skin, 3);
            hm.AddSphere(new Vector3(0, 0.07f, 0.08f), new Vector3(0.16f, 0.13f, 0.12f), beard, 3); // barba
            hm.AddSphere(new Vector3(0, 0.15f, 0.165f), new Vector3(0.1f, 0.025f, 0.03f), beard); // bigode
            hm.AddSphere(new Vector3(0, 0.19f, 0.175f), new Vector3(0.035f, 0.04f, 0.035f), skin * 0.95f); // nariz
            foreach (float x in new[] { -0.065f, 0.065f })
            {
                hm.AddSphere(new Vector3(x, 0.24f, 0.148f), Vector3.one * 0.022f, C(0.1f, 0.08f, 0.07f)); // olhos
                hm.AddSphere(new Vector3(x, 0.285f, 0.15f), new Vector3(0.04f, 0.012f, 0.015f), beard); // sobrancelha
            }
            hm.AddSphere(new Vector3(0.17f, 0.17f, 0), Vector3.one * 0.03f, gold); // brinco
            // Chapéu de pirata (tricórnio)
            hm.AddSphere(new Vector3(0, 0.33f, 0), new Vector3(0.19f, 0.11f, 0.19f), hat, 3);
            hm.AddSphere(new Vector3(0, 0.31f, 0), new Vector3(0.36f, 0.04f, 0.33f), hat, 3);
            for (int i = 0; i < 3; i++)
            {
                float a = (i * 120f + 90f) * Mathf.Deg2Rad;
                Vector3 d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                hm.AddSphere(new Vector3(0, 0.36f, 0) + d * 0.27f, new Vector3(0.12f, 0.08f, 0.12f), hat);
            }
            hm.AddCylinder(new Vector3(0, 0.29f, 0), new Vector3(0, 0.33f, 0), 0.195f, 0.195f, 18, gold, false);
            Mesh(head, hm);
        }

        void Update()
        {
            t += Time.deltaTime;
            if (body == null) return;
            // Respira e olha para o jogador perto.
            body.localScale = new Vector3(1f, 1f + Mathf.Sin(t * 2f) * 0.012f, 1f);
            var game = Game.Instance;
            if (game != null && game.Player != null)
            {
                Vector3 to = game.Player.transform.position - head.position;
                if (to.magnitude < 10f)
                {
                    Vector3 local = transform.InverseTransformDirection(to);
                    float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -60f, 60f);
                    head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(0, yaw, 0), Time.deltaTime * 4f);
                    armR.localRotation = Quaternion.Euler(0, 0, 25f + Mathf.Sin(t * 6f) * 20f * Mathf.Clamp01(1f - to.magnitude / 6f));
                    return;
                }
            }
            head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(0, Mathf.Sin(t * 0.4f) * 35f, 0), Time.deltaTime * 2f);
            armR.localRotation = Quaternion.Slerp(armR.localRotation, Quaternion.identity, Time.deltaTime * 3f);
        }

        public void Interact(PlayerController player)
        {
            Game.Instance.Hud.OpenShop(this);
        }

        public string Greeting => greetings[Random.Range(0, greetings.Length)];
    }

    /// <summary>Faz o balcão abrir a loja também.</summary>
    public class MerchantProxy : MonoBehaviour, IInteractable
    {
        public Merchant Target;
        public string Prompt => Target.Prompt;
        public bool CanInteract => true;
        public void Interact(PlayerController player) => Target.Interact(player);
    }
}
