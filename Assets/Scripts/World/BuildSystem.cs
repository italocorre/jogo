using System.Collections.Generic;
using UnityEngine;

namespace MarAberto
{
    /// <summary>
    /// Construção com o martelo (estilo Raft): prévia translúcida, encaixe nas peças, clique constrói,
    /// R gira, botão direito escolhe a peça, X desmonta (devolve os materiais).
    /// </summary>
    public class BuildSystem : MonoBehaviour
    {
        public const float Range = 7f;
        public PieceKind Selected = PieceKind.Piso;
        public bool Active { get; private set; }
        public bool CanPlace { get; private set; }
        public string Reason { get; private set; }
        public BuildPiece Aimed { get; private set; }

        Game game;
        Transform root;
        GameObject ghost;
        PieceKind ghostKind;
        Material ghostMat;
        int rotSteps;
        Vector3 placePos;
        Quaternion placeRot;
        static readonly Collider[] overlap = new Collider[16];

        public void Init(Game g)
        {
            game = g;
            root = new GameObject("Construcoes").transform;
            ghostMat = new Material(Shader.Find("MarAberto/Ghost")) { name = "Previa" };
        }

        public Recipe RecipeFor(PieceKind k)
        {
            foreach (var r in Crafting.All) if (r.Piece.HasValue && r.Piece.Value == k) return r;
            return null;
        }

        public void Select(PieceKind k)
        {
            Selected = k;
            rotSteps = 0;
        }

        void Update()
        {
            var p = game.Player;
            bool hammer = p != null && game.Inventory.SelectedStack.Type == ItemType.Martelo && p.Driving == null;
            Active = hammer && !p.InputLocked;
            if (!hammer)
            {
                HideGhost();
                Aimed = null;
                return;
            }
            if (p.InputLocked) { HideGhost(); return; }

            var cam = p.Cam.transform;
            // Botão direito: escolher peça.
            if (Input.GetMouseButtonDown(1)) { game.Hud.OpenBuildPicker(); return; }
            if (Input.GetKeyDown(KeyCode.R)) rotSteps = (rotSteps + 1) % 8;

            int mask = ~((1 << Layers.Player) | (1 << Layers.Items) | (1 << Layers.Grass) | (1 << Layers.IgnoreRaycast) | (1 << Layers.UI));
            bool hasHit = Physics.Raycast(cam.position, cam.forward, out var hit, Range, mask, QueryTriggerInteraction.Ignore);
            Aimed = hasHit ? hit.collider.GetComponentInParent<BuildPiece>() : null;

            // X: desmontar a peça mirada.
            if (Aimed != null && Input.GetKeyDown(KeyCode.X)) { Dismantle(Aimed); return; }

            var recipe = RecipeFor(Selected);
            ShowGhost(Selected);
            if (!hasHit) { ghost.SetActive(false); CanPlace = false; Reason = "Mire num lugar perto"; return; }

            ComputePlacement(hit, cam);
            ghost.SetActive(true);
            ghost.transform.SetPositionAndRotation(placePos, placeRot);

            CanPlace = Reason == null && Check(recipe);
            ghostMat.color = CanPlace ? new Color(0.45f, 1f, 0.55f, 0.35f) : new Color(1f, 0.35f, 0.3f, 0.35f);
            if (CanPlace && Input.GetMouseButtonDown(0)) Place(recipe);
        }

        bool Check(Recipe r)
        {
            if (r == null) { Reason = "Peça desconhecida"; return false; }
            if (!Crafting.IsUnlocked(r)) { Reason = "Pesquise no Tab (" + Crafting.CategoryName(r.Category) + ")"; return false; }
            if (!Crafting.Has(game.Inventory, r.Cost)) { Reason = "Faltam materiais"; return false; }
            // Não pode entrar dentro de outra peça.
            Vector3 size = Building.Size(Selected) - Vector3.one * 0.12f;
            Vector3 center = placePos + placeRot * new Vector3(0, Building.Size(Selected).y * 0.5f, 0);
            int n = Physics.OverlapBoxNonAlloc(center, Vector3.Max(size * 0.5f, Vector3.one * 0.02f), overlap, placeRot, 1 << Layers.Build, QueryTriggerInteraction.Ignore);
            if (n > 0) { Reason = "Já tem algo aqui"; return false; }
            // Nem dentro do jogador.
            var p = game.Player.transform.position;
            if (!Building.IsFurniture(Selected) && Selected != PieceKind.Piso && Selected != PieceKind.Telhado)
            {
                var b = new Bounds(center, placeRot * size);
                b.size = new Vector3(Mathf.Abs(b.size.x), Mathf.Abs(b.size.y), Mathf.Abs(b.size.z));
                if (b.Contains(p + Vector3.up * 0.9f)) { Reason = "Saia da frente"; return false; }
            }
            return true;
        }

        static Vector3 Snap4(Vector3 localDir)
        {
            return Mathf.Abs(localDir.x) > Mathf.Abs(localDir.z) ? new Vector3(Mathf.Sign(localDir.x), 0, 0) : new Vector3(0, 0, Mathf.Sign(localDir.z));
        }

        void ComputePlacement(RaycastHit hit, Transform cam)
        {
            Reason = null;
            var piece = hit.collider.GetComponentInParent<BuildPiece>();
            float yaw = Mathf.Round(cam.eulerAngles.y / 90f) * 90f;
            Quaternion freeRot = Quaternion.Euler(0, yaw + rotSteps * 45f, 0);
            float h = Building.Cell * 0.5f;

            switch (Selected)
            {
                case PieceKind.Piso:
                case PieceKind.Telhado:
                {
                    bool roof = Selected == PieceKind.Telhado;
                    if (piece != null && piece.IsFloorLike)
                    {
                        // Ao lado de outro piso, na mesma altura.
                        Vector3 local = Quaternion.Inverse(piece.transform.rotation) * (hit.point - piece.transform.position);
                        Vector3 dir = Snap4(local);
                        placeRot = piece.transform.rotation;
                        placePos = piece.transform.position + piece.transform.rotation * (dir * Building.Cell);
                        if (roof && hit.normal.y > 0.7f && Mathf.Abs(local.x) < h * 0.5f && Mathf.Abs(local.z) < h * 0.5f)
                            placePos = piece.transform.position + Vector3.up * (Building.WallH + Building.FloorH); // andar de cima
                    }
                    else if (piece != null && (piece.Kind == PieceKind.Parede || piece.Kind == PieceKind.Pilar))
                    {
                        // Em cima da parede: telhado/piso do lado de onde você está.
                        Vector3 n = piece.transform.forward;
                        if (Vector3.Dot(cam.position - piece.transform.position, n) < 0) n = -n;
                        if (piece.Kind == PieceKind.Pilar) n = Snap4(Quaternion.Inverse(piece.transform.rotation) * (cam.position - piece.transform.position));
                        placeRot = Quaternion.LookRotation(piece.Kind == PieceKind.Pilar ? piece.transform.rotation * n : n) * Quaternion.identity;
                        placeRot = Quaternion.Euler(0, Mathf.Round(placeRot.eulerAngles.y / 90f) * 90f, 0);
                        Vector3 edgeDir = piece.Kind == PieceKind.Pilar ? piece.transform.rotation * n : n;
                        placePos = piece.transform.position + Vector3.up * Building.WallH + edgeDir.normalized * h;
                        if (piece.Kind == PieceKind.Pilar) placePos += Quaternion.Euler(0, 90, 0) * edgeDir.normalized * h;
                    }
                    else if (piece != null && piece.Kind == PieceKind.Escada)
                    {
                        placeRot = piece.transform.rotation;
                        placePos = piece.transform.position + piece.transform.forward * Building.Cell + Vector3.up * (Building.WallH);
                    }
                    else
                    {
                        if (hit.normal.y < 0.6f) { Reason = "Chão muito inclinado"; }
                        // Grade do mundo, rente ao chão.
                        float gx = Mathf.Round(hit.point.x / Building.Cell) * Building.Cell;
                        float gz = Mathf.Round(hit.point.z / Building.Cell) * Building.Cell;
                        placeRot = Quaternion.identity;
                        placePos = new Vector3(gx, hit.point.y + 0.05f, gz);
                        if (roof) Reason = "O telhado vai em cima de paredes ou pilares";
                        if (placePos.y < Ocean.SeaLevel - 0.1f) Reason = "Na água só com jangada (próxima etapa)";
                    }
                    break;
                }
                case PieceKind.Parede:
                {
                    if (piece != null && piece.IsFloorLike)
                    {
                        Vector3 local = Quaternion.Inverse(piece.transform.rotation) * (hit.point - piece.transform.position);
                        Vector3 dir = Snap4(local);
                        placeRot = piece.transform.rotation * Quaternion.LookRotation(dir);
                        placePos = piece.transform.position + piece.transform.rotation * (dir * h) + Vector3.up * Building.FloorH;
                    }
                    else if (piece != null && piece.Kind == PieceKind.Parede)
                    {
                        placeRot = piece.transform.rotation;
                        placePos = piece.transform.position + Vector3.up * Building.WallH;
                    }
                    else { Reason = "A parede vai na borda de um piso"; placeRot = freeRot; placePos = hit.point; }
                    break;
                }
                case PieceKind.Pilar:
                {
                    if (piece != null && piece.IsFloorLike)
                    {
                        Vector3 local = Quaternion.Inverse(piece.transform.rotation) * (hit.point - piece.transform.position);
                        Vector3 corner = new Vector3(Mathf.Sign(local.x) * h, 0, Mathf.Sign(local.z) * h);
                        placeRot = piece.transform.rotation;
                        placePos = piece.transform.position + piece.transform.rotation * corner + Vector3.up * Building.FloorH;
                    }
                    else if (piece != null && piece.Kind == PieceKind.Pilar)
                    {
                        placeRot = piece.transform.rotation;
                        placePos = piece.transform.position + Vector3.up * Building.WallH;
                    }
                    else { Reason = "O pilar vai no canto de um piso"; placeRot = freeRot; placePos = hit.point; }
                    break;
                }
                case PieceKind.Escada:
                {
                    if (piece != null && piece.IsFloorLike && hit.normal.y > 0.7f)
                    {
                        placeRot = piece.transform.rotation * Quaternion.Euler(0, (rotSteps % 4) * 90f, 0);
                        placePos = piece.transform.position + Vector3.up * Building.FloorH;
                    }
                    else { Reason = "A escada vai em cima de um piso"; placeRot = freeRot; placePos = hit.point; }
                    break;
                }
                default:
                {
                    // Móveis e máquinas: em qualquer superfície plana.
                    placeRot = freeRot;
                    placePos = hit.point;
                    if (hit.normal.y < 0.75f) Reason = "Precisa de um lugar plano";
                    if (placePos.y < Ocean.SeaLevel - 0.1f && piece == null) Reason = "Não dá para construir na água";
                    break;
                }
            }
        }

        void Place(Recipe r)
        {
            foreach (var (t, n) in r.Cost) game.Inventory.Remove(t, n);
            var go = Building.Create(Selected, placePos, placeRot, root);
            var bp = go.GetComponent<BuildPiece>();
            bp.Recipe = r;
            Sfx.Play(Sfx.Hit, placePos, 0.8f);
            Debris.Burst(placePos + Vector3.up * 0.3f, Vector3.up, 6, new Color(0.6f, 0.45f, 0.3f));
            if (Selected == PieceKind.Cama && Bed.Current == null)
            {
                var bed = go.GetComponent<Bed>();
                if (bed != null) bed.Interact(game.Player);
            }
        }

        void Dismantle(BuildPiece bp)
        {
            var r = bp.Recipe ?? RecipeFor(bp.Kind);
            var chest = bp.GetComponent<Chest>();
            if (chest != null) chest.SpillContents();
            if (r != null)
                foreach (var (t, n) in r.Cost)
                {
                    int left = game.Inventory.Add(t, n, false);
                    if (left > 0) WorldItem.Spawn(new ItemStack(t, left), bp.transform.position + Vector3.up, Vector3.up * 2f);
                }
            Sfx.Play(Sfx.Break, bp.transform.position, 0.8f);
            game.Hud.Toast(Building.Name(bp.Kind) + " desmontado: materiais devolvidos.", Hud.Info);
            Destroy(bp.gameObject);
            Aimed = null;
        }

        void ShowGhost(PieceKind k)
        {
            if (ghost != null && ghostKind == k) return;
            if (ghost != null) Destroy(ghost);
            ghost = Building.CreateGhost(k, ghostMat);
            ghostKind = k;
        }

        void HideGhost()
        {
            if (ghost != null) ghost.SetActive(false);
        }
    }
}
