using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>Personagens animados (Quaternius, CC0) que o jogador escolhe no menu.</summary>
    public static class CharacterSkins
    {
        public static readonly string[] Files = { "Pirate_Male", "Pirate_Female", "Viking_Male", "Viking_Female", "Worker_Male", "Worker_Female", "Cowboy_Male", "Cowboy_Female", "Casual_Male", "Casual_Female" };
        public static readonly string[] Names = { "Pirata", "Pirata", "Viking", "Viking", "Trabalhador", "Trabalhadora", "Cowboy", "Cowgirl", "Marujo", "Marujo" };
        public static int Count => Files.Length;
        public static string Label(int i) { i = ((i % Count) + Count) % Count; return Names[i] + (i % 2 == 0 ? " (homem)" : " (mulher)"); }
    }

    /// <summary>Outro jogador visto na tela: personagem animado com item na mão e nome em cima.</summary>
    public class RemotePlayer : MonoBehaviour
    {
        public static readonly List<RemotePlayer> All = new List<RemotePlayer>();
        public int Id;
        public string PlayerName;
        Vector3 netPos;
        float netYaw, netPitch;
        bool hasState, swimming, driving;
        Transform head, body, handItem;
        MeshFilter heldFilter;
        MeshRenderer heldRenderer;
        ItemType held = ItemType.Nada;
        float walkT;

        static readonly Color[] shirts =
        {
            new Color(0.72f, 0.22f, 0.18f), new Color(0.20f, 0.38f, 0.62f), new Color(0.25f, 0.52f, 0.30f), new Color(0.80f, 0.62f, 0.20f),
            new Color(0.48f, 0.30f, 0.60f), new Color(0.15f, 0.55f, 0.58f), new Color(0.85f, 0.45f, 0.20f), new Color(0.35f, 0.35f, 0.38f),
        };

        Animation anim;
        Transform model;
        string clipIdle, clipWalk, clipPunch, clipHit, clipSit;
        float actionUntil;

        public static RemotePlayer Create(int id, string name, int skin = 0)
        {
            var go = new GameObject("Jogador_" + name);
            go.layer = Layers.Remote;
            var rp = go.AddComponent<RemotePlayer>();
            rp.Id = id;
            rp.PlayerName = name;
            if (!rp.BuildCharacter(skin)) rp.Build(shirts[Mathf.Abs(id) % shirts.Length]);
            foreach (var t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Remote;
            go.SetActive(false); // aparece quando chegar a primeira posição
            return rp;
        }

        /// <summary>Personagem animado; devolve false se o modelo não existir.</summary>
        bool BuildCharacter(int skin)
        {
            skin = ((skin % CharacterSkins.Count) + CharacterSkins.Count) % CharacterSkins.Count;
            var prefab = Resources.Load<GameObject>("Characters/" + CharacterSkins.Files[skin]);
            if (prefab == null) return false;
            var inst = Instantiate(prefab, transform, false);
            inst.name = "Modelo";
            model = inst.transform;
            // Altura de ~1,75 m e pé no chão.
            var rends = inst.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);
                float h = Mathf.Max(0.01f, b.size.y);
                model.localScale *= 1.75f / h;
                b = rends[0].bounds; foreach (var r in rends) b.Encapsulate(r.bounds);
                model.position += Vector3.up * (transform.position.y - b.min.y);
                foreach (var r in rends) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On; if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true; }
            }
            anim = inst.GetComponentInChildren<Animation>();
            if (anim != null)
            {
                foreach (AnimationState st in anim)
                {
                    string n = st.name;
                    if (n.EndsWith("|Idle") || n == "Idle") { clipIdle = n; st.wrapMode = WrapMode.Loop; }
                    else if (n.EndsWith("|Walk") || n == "Walk") { clipWalk = n; st.wrapMode = WrapMode.Loop; }
                    else if (n.Contains("Punch")) { clipPunch = n; st.wrapMode = WrapMode.Once; }
                    else if (n.Contains("RecieveHit")) { clipHit = n; st.wrapMode = WrapMode.Once; }
                    else if (n.Contains("SitDown")) { clipSit = n; st.wrapMode = WrapMode.ClampForever; }
                }
                if (clipIdle != null) anim.Play(clipIdle);
            }
            // Item na mão direita.
            Transform hand = null;
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("hand") && (n.EndsWith("r") || n.EndsWith(".r") || n.EndsWith("_r") || n.Contains("right")) && !n.Contains("index") && !n.Contains("thumb")) { hand = t; break; }
            }
            handItem = new GameObject("Item").transform;
            handItem.SetParent(hand != null ? hand : model, false);
            handItem.localPosition = Vector3.zero;
            handItem.localRotation = Quaternion.identity;
            // Compensa a escala do esqueleto para o item ficar do tamanho certo.
            Vector3 ls = handItem.lossyScale;
            handItem.localScale = new Vector3(1f / Mathf.Max(ls.x, 1e-4f), 1f / Mathf.Max(ls.y, 1e-4f), 1f / Mathf.Max(ls.z, 1e-4f));
            heldFilter = handItem.gameObject.AddComponent<MeshFilter>();
            heldRenderer = handItem.gameObject.AddComponent<MeshRenderer>();
            heldRenderer.enabled = false;
            body = model;
            head = model;
            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.9f, 0);
            col.height = 1.8f;
            col.radius = 0.32f;
            return true;
        }

        /// <summary>Pose parada (para os prints, onde a animação não roda sozinha).</summary>
        public void PreviewPose(float t = 0.5f)
        {
            if (anim == null || clipIdle == null) return;
            anim.Play(clipIdle);
            anim[clipIdle].time = t;
            anim.Sample();
        }

        /// <summary>Animação de golpe (soco/ferramenta) ou de levar golpe.</summary>
        public void PlayAction(bool hit)
        {
            if (anim == null) return;
            string c = hit ? clipHit : clipPunch;
            if (c == null) return;
            anim.CrossFade(c, 0.08f);
            actionUntil = Time.time + 0.6f;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Build(Color shirt)
        {
            Color skin = new Color(0.82f, 0.62f, 0.47f), pants = new Color(0.22f, 0.24f, 0.30f), boots = new Color(0.25f, 0.17f, 0.10f), hat = new Color(0.30f, 0.22f, 0.14f);
            var mb = new MeshBuilder();
            mb.AddCapsule(new Vector3(-0.11f, 0.42f - 0.42f, 0), new Vector3(-0.11f, 0.42f + 0.42f, 0), 0.08f, pants, 10);
            mb.AddCapsule(new Vector3(0.11f, 0.42f - 0.42f, 0), new Vector3(0.11f, 0.42f + 0.42f, 0), 0.08f, pants, 10);
            mb.AddCapsule(new Vector3(-0.11f, 0.06f - 0.06f, 0.04f), new Vector3(-0.11f, 0.06f + 0.06f, 0.04f), 0.05f, boots, 8);
            mb.AddCapsule(new Vector3(0.11f, 0.06f - 0.06f, 0.04f), new Vector3(0.11f, 0.06f + 0.06f, 0.04f), 0.05f, boots, 8);
            var bodyGo = new GameObject("Corpo");
            bodyGo.transform.SetParent(transform, false);
            bodyGo.AddComponent<MeshFilter>().sharedMesh = mb.Build("PernasRemoto");
            bodyGo.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;

            var tb = new MeshBuilder();
            tb.AddCapsule(new Vector3(0, 1.15f - 0.33f, 0), new Vector3(0, 1.15f + 0.33f, 0), 0.12f, shirt, 12);
            tb.AddCapsule(new Vector3(-0.3f, 1.1f - 0.3f, 0), new Vector3(-0.3f, 1.1f + 0.3f, 0), 0.06f, shirt, 8);
            tb.AddCapsule(new Vector3(0.3f, 1.1f - 0.3f, 0.05f), new Vector3(0.3f, 1.1f + 0.3f, 0.05f), 0.06f, shirt, 8);
            tb.AddSphere(new Vector3(-0.3f, 0.78f, 0), Vector3.one * 0.06f, skin);
            tb.AddSphere(new Vector3(0.3f, 0.8f, 0.1f), Vector3.one * 0.06f, skin);
            body = new GameObject("Tronco").transform;
            body.SetParent(transform, false);
            body.gameObject.AddComponent<MeshFilter>().sharedMesh = tb.Build("TroncoRemoto");
            body.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;

            var hb = new MeshBuilder();
            hb.AddSphere(Vector3.zero, new Vector3(0.13f, 0.15f, 0.14f), skin, 3);
            hb.AddSphere(new Vector3(-0.05f, 0.02f, 0.12f), Vector3.one * 0.018f, new Color(0.1f, 0.1f, 0.1f), 1);
            hb.AddSphere(new Vector3(0.05f, 0.02f, 0.12f), Vector3.one * 0.018f, new Color(0.1f, 0.1f, 0.1f), 1);
            hb.AddCylinder(new Vector3(0, 0.08f, 0), new Vector3(0, 0.1f, 0), 0.24f, 0.24f, 16, hat);   // aba do chapéu
            hb.AddCylinder(new Vector3(0, 0.1f, 0), new Vector3(0, 0.22f, 0), 0.13f, 0.11f, 14, hat);
            head = new GameObject("Cabeca").transform;
            head.SetParent(transform, false);
            head.localPosition = new Vector3(0, 1.62f, 0);
            head.gameObject.AddComponent<MeshFilter>().sharedMesh = hb.Build("CabecaRemoto");
            head.gameObject.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;

            handItem = new GameObject("Item").transform;
            handItem.SetParent(body, false);
            handItem.localPosition = new Vector3(0.3f, 0.8f, 0.12f);
            handItem.localRotation = Quaternion.Euler(70, 0, 0);
            heldFilter = handItem.gameObject.AddComponent<MeshFilter>();
            heldRenderer = handItem.gameObject.AddComponent<MeshRenderer>();
            heldRenderer.enabled = false;

            var col = gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.9f, 0);
            col.height = 1.8f;
            col.radius = 0.3f;
        }

        public void SetState(Vector3 pos, float yaw, float pitch, ItemType item, bool swim, bool drive)
        {
            netPos = pos; netYaw = yaw; netPitch = pitch; swimming = swim; driving = drive;
            if (!hasState) { transform.position = pos; hasState = true; gameObject.SetActive(true); }
            if (item != held)
            {
                held = item;
                heldRenderer.enabled = item != ItemType.Nada;
                if (item != ItemType.Nada)
                {
                    heldFilter.sharedMesh = ItemModels.Get(item);
                    heldRenderer.sharedMaterial = ItemModels.Material(item);
                }
            }
        }

        void Update()
        {
            if (!hasState) return;
            float k = 1f - Mathf.Exp(-Time.deltaTime * 12f);
            Vector3 before = transform.position;
            transform.position = Vector3.Lerp(transform.position, netPos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, netYaw, 0), k);
            if (anim == null) head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(Mathf.DeltaAngle(0, netPitch) * 0.7f, 0, 0), k);
            // Balanço simples ao andar; deitado ao nadar.
            float speed = (transform.position - before).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            walkT += Time.deltaTime * Mathf.Clamp(speed, 0, 6f) * 2.2f;
            if (anim != null)
            {
                if (Time.time > actionUntil)
                {
                    if (driving && clipSit != null) { if (!anim.IsPlaying(clipSit)) anim.CrossFade(clipSit, 0.2f); }
                    else if ((speed > 0.25f || swimming) && clipWalk != null)
                    {
                        anim[clipWalk].speed = swimming ? 0.6f : Mathf.Clamp(speed / 1.6f, 0.6f, 2.2f);
                        if (!anim.IsPlaying(clipWalk)) anim.CrossFade(clipWalk, 0.2f);
                    }
                    else if (clipIdle != null && !anim.IsPlaying(clipIdle)) anim.CrossFade(clipIdle, 0.25f);
                }
                // Nadando: corpo deitado na água.
                model.localRotation = Quaternion.Slerp(model.localRotation, swimming ? Quaternion.Euler(70, 0, 0) : Quaternion.identity, k);
                return;
            }
            float bob = speed > 0.3f && !swimming && !driving ? Mathf.Abs(Mathf.Sin(walkT)) * 0.05f : 0f;
            body.localPosition = new Vector3(0, bob, 0);
            body.localRotation = Quaternion.Slerp(body.localRotation, swimming ? Quaternion.Euler(60, 0, 0) : Quaternion.identity, k);
        }
    }
}
