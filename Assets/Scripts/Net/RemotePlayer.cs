using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>Outro jogador visto na tela: boneco simples com a cor dele, item na mão e nome em cima.</summary>
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

        public static RemotePlayer Create(int id, string name)
        {
            var go = new GameObject("Jogador_" + name);
            go.layer = Layers.Player;
            var rp = go.AddComponent<RemotePlayer>();
            rp.Id = id;
            rp.PlayerName = name;
            rp.Build(shirts[Mathf.Abs(id) % shirts.Length]);
            go.SetActive(false); // aparece quando chegar a primeira posição
            return rp;
        }

        void OnEnable() => All.Add(this);
        void OnDisable() => All.Remove(this);

        void Build(Color shirt)
        {
            Color skin = new Color(0.82f, 0.62f, 0.47f), pants = new Color(0.22f, 0.24f, 0.30f), boots = new Color(0.25f, 0.17f, 0.10f), hat = new Color(0.30f, 0.22f, 0.14f);
            var mb = new MeshBuilder();
            mb.AddCapsule(new Vector3(-0.11f, 0.42f, 0), new Vector3(0.08f, 0.42f, 0.08f), 0.08f, pants, 10);
            mb.AddCapsule(new Vector3(0.11f, 0.42f, 0), new Vector3(0.08f, 0.42f, 0.08f), 0.08f, pants, 10);
            mb.AddCapsule(new Vector3(-0.11f, 0.06f, 0.04f), new Vector3(0.08f, 0.06f, 0.13f), 0.05f, boots, 8);
            mb.AddCapsule(new Vector3(0.11f, 0.06f, 0.04f), new Vector3(0.08f, 0.06f, 0.13f), 0.05f, boots, 8);
            var bodyGo = new GameObject("Corpo");
            bodyGo.transform.SetParent(transform, false);
            bodyGo.AddComponent<MeshFilter>().sharedMesh = mb.Build("PernasRemoto");
            bodyGo.AddComponent<MeshRenderer>().sharedMaterial = Mats.LowPoly;

            var tb = new MeshBuilder();
            tb.AddCapsule(new Vector3(0, 1.15f, 0), new Vector3(0.24f, 0.33f, 0.15f), 0.12f, shirt, 12);
            tb.AddCapsule(new Vector3(-0.3f, 1.1f, 0), new Vector3(0.06f, 0.3f, 0.06f), 0.06f, shirt, 8);
            tb.AddCapsule(new Vector3(0.3f, 1.1f, 0.05f), new Vector3(0.06f, 0.3f, 0.06f), 0.06f, shirt, 8);
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
            head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(Mathf.DeltaAngle(0, netPitch) * 0.7f, 0, 0), k);
            // Balanço simples ao andar; deitado ao nadar.
            float speed = (transform.position - before).magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            walkT += Time.deltaTime * Mathf.Clamp(speed, 0, 6f) * 2.2f;
            float bob = speed > 0.3f && !swimming && !driving ? Mathf.Abs(Mathf.Sin(walkT)) * 0.05f : 0f;
            body.localPosition = new Vector3(0, bob, 0);
            body.localRotation = Quaternion.Slerp(body.localRotation, swimming ? Quaternion.Euler(60, 0, 0) : Quaternion.identity, k);
        }
    }
}
