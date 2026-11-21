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

            var mb = new MeshBuilder();
            // Chão de tábuas
            for (int i = 0; i < 7; i++)
                mb.AddBox(new Vector3(-1.95f + i * 0.65f, 0.05f, -0.6f), new Vector3(0.6f, 0.1f, 3.4f), Quaternion.identity, i % 2 == 0 ? wood : woodDark * 1.15f);
            // Postes
            foreach (var p in new[] { new Vector3(-2.1f, 0, 0.9f), new Vector3(2.1f, 0, 0.9f), new Vector3(-2.1f, 0, -2.1f), new Vector3(2.1f, 0, -2.1f) })
                mb.AddCylinder(p, p + Vector3.up * (p.z > 0 ? 2.6f : 3.1f), 0.09f, 0.08f, 6, woodDark);
            // Toldo listrado (inclinado para a frente)
            int stripes = 8;
            for (int i = 0; i < stripes; i++)
            {
                float x0 = -2.4f + i * 4.8f / stripes, x1 = x0 + 4.8f / stripes;
                Color c = i % 2 == 0 ? red : cream;
                Vector3 a = new Vector3(x0, 3.15f, -2.4f), b = new Vector3(x0, 2.55f, 1.3f);
                Vector3 cc = new Vector3(x1, 2.55f, 1.3f), d = new Vector3(x1, 3.15f, -2.4f);
                mb.AddQuad(a, d, cc, b, c * 0.8f);   // embaixo
                mb.AddQuad(b, cc, d, a, c);          // em cima
                // Franja ondulada
                Vector3 fa = new Vector3(x0, 2.55f, 1.3f), fb = new Vector3(x1, 2.55f, 1.3f), fm = new Vector3((x0 + x1) / 2, 2.3f, 1.31f);
                mb.AddTri(fa, fb, fm, c);
                mb.AddTri(fb, fa, fm, c * 0.85f);
            }
            // Caixotes e barril
            mb.AddBox(new Vector3(-2.7f, 0.35f, 0.3f), new Vector3(0.7f, 0.7f, 0.7f), Quaternion.Euler(0, 12, 0), wood);
            mb.AddBox(new Vector3(-2.65f, 0.95f, 0.25f), new Vector3(0.5f, 0.5f, 0.5f), Quaternion.Euler(0, -8, 0), woodDark * 1.2f);
            mb.AddBox(new Vector3(2.75f, 0.3f, -0.4f), new Vector3(0.6f, 0.6f, 0.6f), Quaternion.Euler(0, 30, 0), wood);
            mb.AddCylinder(new Vector3(2.7f, 0, 0.6f), new Vector3(2.7f, 0.9f, 0.6f), 0.35f, 0.35f, 9, woodDark * 1.1f);
            mb.AddCylinder(new Vector3(2.7f, 0.25f, 0.6f), new Vector3(2.7f, 0.32f, 0.6f), 0.37f, 0.37f, 9, C(0.35f, 0.35f, 0.38f), false);
            mb.AddCylinder(new Vector3(2.7f, 0.62f, 0.6f), new Vector3(2.7f, 0.69f, 0.6f), 0.37f, 0.37f, 9, C(0.35f, 0.35f, 0.38f), false);
            // Mercadorias em cima do caixote: minérios coloridos
            mb.AddRock(new Vector3(-2.7f, 0.85f, 0.45f), 0.12f, 3, _ => C(0.9f, 0.5f, 0.25f), 0.2f);
            mb.AddRock(new Vector3(-2.55f, 1.28f, 0.2f), 0.1f, 4, _ => C(0.2f, 0.2f, 0.22f), 0.2f);
            // Lanterna pendurada
            mb.AddCylinder(new Vector3(1.6f, 2.55f, 1.0f), new Vector3(1.6f, 2.38f, 1.0f), 0.01f, 0.01f, 6, C(0.2f, 0.2f, 0.2f), false);
            mb.AddSphere(new Vector3(1.6f, 2.22f, 1.0f), new Vector3(0.11f, 0.15f, 0.11f), C(1f, 0.86f, 0.5f), 2);
            mb.AddCylinder(new Vector3(1.6f, 2.36f, 1.0f), new Vector3(1.6f, 2.42f, 1.0f), 0.09f, 0.05f, 10, C(0.25f, 0.22f, 0.2f));
            mb.AddCylinder(new Vector3(1.6f, 2.04f, 1.0f), new Vector3(1.6f, 2.09f, 1.0f), 0.1f, 0.1f, 10, C(0.25f, 0.22f, 0.2f));

            var go = new GameObject("Barraca");
            go.transform.SetParent(root, false);
            var mesh = mb.Build("Barraca");
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            var bcol = go.AddComponent<BoxCollider>();
            bcol.center = new Vector3(0, 0.05f, -0.6f);
            bcol.size = new Vector3(4.4f, 0.1f, 3.4f);

            // Balcão (separado, para ser clicável)
            var cmb = new MeshBuilder();
            cmb.AddBox(new Vector3(0, 0.55f, 0.6f), new Vector3(3.6f, 1.0f, 0.7f), Quaternion.identity, wood);
            cmb.AddBox(new Vector3(0, 1.08f, 0.62f), new Vector3(3.8f, 0.08f, 0.85f), Quaternion.identity, woodDark * 1.2f);
            for (int i = 0; i < 5; i++)
                cmb.AddBox(new Vector3(-1.6f + i * 0.8f, 0.55f, 0.96f), new Vector3(0.06f, 0.95f, 0.02f), Quaternion.identity, woodDark);
            // Moedas e conchas no balcão
            for (int i = 0; i < 6; i++)
                cmb.AddCylinder(new Vector3(0.6f + i * 0.03f, 1.12f + i * 0.025f, 0.6f), new Vector3(0.6f + i * 0.03f, 1.14f + i * 0.025f, 0.6f), 0.07f, 0.07f, 8, C(1f, 0.8f, 0.25f));
            var counter = new GameObject("Balcao");
            counter.transform.SetParent(root, false);
            var cmesh = cmb.Build("Balcao");
            counter.AddComponent<MeshFilter>().sharedMesh = cmesh;
            counter.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;
            var cc2 = counter.AddComponent<BoxCollider>();
            cc2.center = new Vector3(0, 0.55f, 0.62f);
            cc2.size = new Vector3(3.8f, 1.1f, 0.85f);
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
