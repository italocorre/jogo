using UnityEngine;

namespace MarAberto
{
    /// <summary>Câmera em 3ª pessoa (V): mostra o seu personagem animado, por cima do ombro.</summary>
    public class ThirdPerson : MonoBehaviour
    {
        public static bool Active { get; private set; }
        PlayerController player;
        RemotePlayer avatar;
        float dist = 3.4f;
        Vector3 smooth;
        static readonly int mask = ~(Layers.PlayerMask | (1 << Layers.Items) | (1 << Layers.Grass) | (1 << Layers.IgnoreRaycast) | (1 << Layers.Remote) | (1 << Layers.UI));

        void Awake() => player = GetComponent<PlayerController>();

        void EnsureAvatar()
        {
            if (avatar != null) return;
            avatar = RemotePlayer.Create(-1, NetConfig.PlayerName, NetConfig.Skin);
            avatar.IsLocal = true;
            avatar.name = "MeuPersonagem";
            // Não atrapalha mira nem colisão.
            foreach (var c in avatar.GetComponentsInChildren<Collider>(true)) Destroy(c);
            foreach (var t in avatar.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.IgnoreRaycast;
        }

        void Update()
        {
            var g = Game.Instance;
            if (g == null || g.Hud == null) return;
            if (Input.GetKeyDown(KeyCode.V) && !g.Hud.AnyPanelOpen && !player.InputLocked)
            {
                Active = !Active;
                EnsureAvatar();
                avatar.gameObject.SetActive(Active);
                g.Hud.Toast(Active ? "Câmera em 3ª pessoa (V volta)" : "Câmera em 1ª pessoa", Hud.Info);
            }
            if (Active && player.Driving == null && Input.GetMouseButtonDown(0) && !player.InputLocked && Cursor.lockState == CursorLockMode.Locked)
                avatar.PlayAction(false);
            if (Active && !player.InputLocked) dist = Mathf.Clamp(dist - Input.mouseScrollDelta.y * (Input.GetKey(KeyCode.LeftAlt) ? 0.4f : 0f), 1.8f, 6f);
        }

        void LateUpdate()
        {
            if (!Active || player == null || player.Cam == null || avatar == null) return;
            var cam = player.Cam.transform;
            var held = Game.Instance.Inventory.SelectedStack;
            avatar.SetState(transform.position, cam.eulerAngles.y, cam.eulerAngles.x, held.IsEmpty ? ItemType.Nada : held.Type, player.Swimming, player.Driving != null);
            // Por cima do ombro direito; encosta na parede sem atravessar.
            Vector3 pivot = transform.position + Vector3.up * (player.Driving != null ? 1.6f : 1.7f);
            Vector3 back = cam.rotation * new Vector3(0.55f, 0.2f, -dist);
            float d = back.magnitude;
            if (Physics.SphereCast(pivot, 0.25f, back / d, out var hit, d, mask, QueryTriggerInteraction.Ignore)) d = Mathf.Max(0.4f, hit.distance - 0.1f);
            Vector3 want = pivot + back.normalized * d;
            smooth = (smooth - want).sqrMagnitude > 25f ? want : Vector3.Lerp(smooth, want, 1f - Mathf.Exp(-Time.deltaTime * 25f));
            cam.position = smooth;
        }

        void OnDisable() => Active = false;
    }
}
