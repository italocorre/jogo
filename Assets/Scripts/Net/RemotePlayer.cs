using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>Personagens (Kenney, CC0): corpo humano normal com roupas diferentes (texturas).</summary>
    public static class CharacterSkins
    {
        public static readonly string[] Files = { "humanMaleA", "humanFemaleA", "survivorMaleB", "survivorFemaleA", "skaterMaleA", "skaterFemaleA", "criminalMaleA" };
        static readonly string[] names = { "Marujo", "Marujo (mulher)", "Sobrevivente", "Sobrevivente (mulher)", "Aventureiro", "Aventureira", "Pirata" };
        public static int Count => Files.Length;
        public static string Label(int i) { i = ((i % Count) + Count) % Count; return names[i]; }
    }

    /// <summary>Outro jogador visto na tela: personagem animado com item na mão e nome em cima.</summary>
    public class RemotePlayer : MonoBehaviour
    {
        public static readonly List<RemotePlayer> All = new List<RemotePlayer>();
        public int Id;
        public string PlayerName;
        public bool IsSwimming => swimming;
        /// <summary>O seu próprio personagem (câmera em 3ª pessoa): não conta como outro jogador.</summary>
        public bool IsLocal;
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
        float actionUntil, swingT = -1f;
        Transform rightArm;

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
            var prefab = Resources.Load<GameObject>("Characters/Kenney/characterMedium");
            if (prefab == null) return false;
            var inst = Instantiate(prefab, transform, false);
            inst.name = "Modelo";
            model = inst.transform;
            // Roupa/pele (textura) com o material realista do jogo.
            var tex = Resources.Load<Texture2D>("Characters/Kenney/Skins/" + CharacterSkins.Files[skin]);
            var mat = ModelLibrary.Custom("Pele_" + CharacterSkins.Files[skin], tex, null, 0.15f);
            var rends = inst.GetComponentsInChildren<Renderer>();
            foreach (var r in rends)
            {
                var ms = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < ms.Length; i++) ms[i] = mat;
                r.sharedMaterials = ms;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = true;
            }
            // Altura de ~1,75 m e pé no chão. (Malha com esqueleto: mede pela malha original,
            // porque os "bounds" do renderer ainda não valem logo depois de criar.)
            float h = 0f, minY = 0f;
            foreach (var r in rends)
            {
                var smr = r as SkinnedMeshRenderer;
                Mesh m = smr != null ? smr.sharedMesh : r.GetComponent<MeshFilter>()?.sharedMesh;
                if (m == null) continue;
                var mb = m.bounds;
                Vector3 lo = r.transform.TransformPoint(mb.min), hi = r.transform.TransformPoint(mb.max);
                float hh = Mathf.Abs(hi.y - lo.y);
                if (hh > h) { h = hh; minY = Mathf.Min(lo.y, hi.y); }
            }
            if (h > 0.01f)
            {
                float k = 1.75f / h;
                model.localScale *= k;
                model.localPosition += Vector3.up * (transform.position.y - minY) * k;
            }
            // Animações (arquivos separados, mesmo esqueleto).
            anim = inst.GetComponent<Animation>();
            if (anim == null) anim = inst.AddComponent<Animation>();
            anim.cullingType = AnimationCullingType.AlwaysAnimate;
            AnimationClip Clip(string file, string ends)
            {
                foreach (var c in Resources.LoadAll<AnimationClip>("Characters/Kenney/" + file))
                    if (!c.name.StartsWith("__preview") && c.name.ToLowerInvariant().EndsWith(ends)) return c;
                return null;
            }
            var idle = Clip("idle", "idle"); var run = Clip("run", "run"); var jump = Clip("jump", "jump");
            if (idle != null) { idle.legacy = true; anim.AddClip(idle, "Idle"); anim["Idle"].wrapMode = WrapMode.Loop; clipIdle = "Idle"; }
            if (run != null) { run.legacy = true; anim.AddClip(run, "Walk"); anim["Walk"].wrapMode = WrapMode.Loop; clipWalk = "Walk"; }
            if (jump != null) { jump.legacy = true; anim.AddClip(jump, "Jump"); anim["Jump"].wrapMode = WrapMode.Once; clipPunch = "Jump"; }
            if (clipIdle != null) anim.Play(clipIdle);
            // Item na mão direita.
            Transform hand = null;
            foreach (var t in inst.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "RightHand") hand = t;
                if (t.name == "RightArm") rightArm = t;
            }
            handItem = new GameObject("Item").transform;
            handItem.SetParent(hand != null ? hand : model, false);
            handItem.localPosition = Vector3.zero;
            handItem.localRotation = Quaternion.Euler(0, 0, 90);
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
            if (hit) { if (clipHit != null) { anim.CrossFade(clipHit, 0.08f); actionUntil = Time.time + 0.6f; } return; }
            swingT = 0f;   // golpe: o braço direito desce (feito por código, por cima da animação)
        }

        void LateUpdate()
        {
            if (swingT < 0 || rightArm == null) return;
            swingT += Time.deltaTime / 0.45f;
            if (swingT >= 1f) { swingT = -1f; return; }
            // Levanta rápido e desce batendo.
            float a = swingT < 0.35f ? Mathf.SmoothStep(0, 1, swingT / 0.35f) : 1f - Mathf.SmoothStep(0, 1, (swingT - 0.35f) / 0.65f);
            rightArm.localRotation = rightArm.localRotation * Quaternion.Euler(-110f * a, 0, -25f * a);
        }

        void OnEnable() { if (!IsLocal) All.Add(this); }
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
            transform.position = IsLocal ? netPos : Vector3.Lerp(transform.position, netPos, k);
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, netYaw, 0), k);
            if (anim == null) head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(Mathf.DeltaAngle(0, netPitch) * 0.7f, 0, 0), k);
            // Balanço simples ao andar; deitado ao nadar.
            float speed = (transform.position - before).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            walkT += Time.deltaTime * Mathf.Clamp(speed, 0, 6f) * 2.2f;
            if (anim != null)
            {
                // Pulo: subindo rápido fora d'água.
                float vy = (transform.position.y - before.y) / Mathf.Max(Time.deltaTime, 0.0001f);
                if (vy > 2.2f && !swimming && !driving && Time.time > actionUntil && clipPunch != null)
                {
                    anim.CrossFade(clipPunch, 0.1f);
                    actionUntil = Time.time + 0.75f;
                }
                if (Time.time > actionUntil)
                {
                    if (driving && clipSit != null) { if (!anim.IsPlaying(clipSit)) anim.CrossFade(clipSit, 0.2f); }
                    else if ((speed > 0.25f || swimming) && clipWalk != null)
                    {
                        anim[clipWalk].speed = swimming ? 0.45f : Mathf.Clamp(speed / 4.5f, 0.45f, 1.4f);
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
