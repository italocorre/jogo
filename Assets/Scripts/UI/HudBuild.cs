using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MarAberto
{
    /// <summary>Construção (escolher peça, dica do martelo) e baú.</summary>
    public partial class Hud
    {
        RectTransform pickerWin, pickerGrid, chestWin, chestGrid, chestBagGrid, buildHint;
        Text buildHintText, chestTitle;
        Chest openChest;
        int chestVersion = -1;

        void BuildExtraPanels()
        {
            // Escolher peça (botão direito com o martelo)
            pickerWin = Window("Construir", new Vector2(980, 560));
            UIKit.Txt(pickerWin, "Titulo", "CONSTRUIR", UIKit.Title, 32, UIKit.Cream, TextAnchor.UpperCenter)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(600, 44));
            UIKit.Txt(pickerWin, "Dica", "Escolha a peça  ·  as bloqueadas se pesquisam no Tab", UIKit.Body, 18, UIKit.Muted, TextAnchor.UpperCenter)
                .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -70), new Vector2(800, 28));
            pickerGrid = UIKit.Rect(pickerWin, "Grade").Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -116), new Vector2(900, 420));

            // Baú
            chestWin = Window("Bau", new Vector2(1240, 640));
            chestTitle = UIKit.Txt(chestWin, "Titulo", "BAÚ", UIKit.Title, 30, UIKit.Cream, TextAnchor.UpperLeft);
            chestTitle.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -24), new Vector2(560, 40));
            UIKit.Txt(chestWin, "Titulo2", "SUA MOCHILA", UIKit.Title, 30, UIKit.Cream, TextAnchor.UpperLeft)
                .rectTransform.Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40 - 520 + 520, -24), new Vector2(520, 40));
            UIKit.Txt(chestWin, "Dica", "Clique num item para passar de um lado para o outro", UIKit.Body, 18, UIKit.Muted, TextAnchor.MiddleCenter)
                .rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(900, 28));
            chestGrid = UIKit.Rect(chestWin, "GradeBau").Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(40, -80), new Vector2(560, 480));
            chestBagGrid = UIKit.Rect(chestWin, "GradeMochila").Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-40, -80), new Vector2(560, 480));
            UIKit.Img(chestWin, "Divisoria", UIKit.White, new Color(UIKit.Brass.r, UIKit.Brass.g, UIKit.Brass.b, 0.3f)).rectTransform
                .Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -80), new Vector2(1, 470));

            // Dica do martelo (acima dos atalhos)
            buildHint = UIKit.Panel(root, "DicaConstrucao").rectTransform;
            buildHint.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 150), new Vector2(760, 74));
            buildHintText = UIKit.Txt(buildHint, "Texto", "", UIKit.Body, 19, UIKit.Text, TextAnchor.MiddleCenter);
            buildHintText.rectTransform.Fill(16, 4, 16, 4);
            buildHint.gameObject.SetActive(false);
        }

        public void OpenBuildPicker()
        {
            SetPanel(Panel.Build);
            RebuildPicker();
        }

        public void OpenChest(Chest c)
        {
            openChest = c;
            chestVersion = -1;
            SetPanel(Panel.Chest);
        }

        /// <summary>Abre o Tab na aba Fabricar, numa categoria (usado pelas máquinas).</summary>
        public void OpenCraft(CraftCategory cat)
        {
            SetPanel(Panel.Backpack);
            tab = Tab.Craft;
            craftCat = cat;
            craftSel = null;
            RefreshTabs();
        }

        void RebuildPicker()
        {
            UIKit.ClearChildren(pickerGrid);
            var bs = game.Build;
            var list = new List<Recipe>();
            foreach (var r in Crafting.All) if (r.Piece.HasValue) list.Add(r);
            const int cols = 6;
            const float w = 140, h = 190, gap = 12;
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                bool unlocked = Crafting.IsUnlocked(r), has = Crafting.Has(game.Inventory, r.Cost);
                bool sel = bs != null && bs.Selected == r.Piece.Value;
                var card = UIKit.Img(pickerGrid, "Peca", sel ? UIKit.SlotSelected : UIKit.Slot, null, true, 1f);
                card.raycastTarget = true;
                card.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2((i % cols) * (w + gap), -(i / cols) * (h + gap)), new Vector2(w, h));
                if (!unlocked) card.color = new Color(1, 1, 1, 0.5f);
                var ic = UIKit.Icon(card.transform, "Icone");
                ic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(76, 76));
                ic.texture = Icons.Get(r.IconItem);
                ic.color = unlocked ? Color.white : new Color(0.5f, 0.5f, 0.55f, 0.8f);
                UIKit.Txt(card.transform, "Nome", r.Name, UIKit.BodyBold, 16, unlocked ? UIKit.Text : UIKit.Muted, TextAnchor.UpperCenter)
                    .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -90), new Vector2(w - 8, 40));
                string cost = "";
                foreach (var (t, n) in r.Cost) cost += n + " " + Items.Get(t).Name.ToLowerInvariant() + "\n";
                UIKit.Txt(card.transform, "Custo", unlocked ? cost.TrimEnd() : "pesquise no Tab", UIKit.Body, 14,
                    !unlocked ? UIKit.BadC : has ? UIKit.GoodC : UIKit.BadC, TextAnchor.UpperCenter)
                    .rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -132), new Vector2(w - 8, 58));
                var b = card.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                var kind = r.Piece.Value;
                b.onClick.AddListener(() =>
                {
                    game.Build.Select(kind);
                    Sfx.UI(Sfx.Page, 0.4f);
                    SetPanel(Panel.None);
                });
            }
        }

        void RefreshBuildHint(bool hudOn)
        {
            var bs = game.Build;
            bool show = hudOn && bs != null && bs.Active;
            buildHint.gameObject.SetActive(show);
            if (!show) return;
            var r = bs.RecipeFor(bs.Selected);
            string cost = "";
            if (r != null)
                foreach (var (t, n) in r.Cost)
                {
                    bool ok = game.Inventory.Count(t) >= n;
                    cost += (cost.Length > 0 ? "  ·  " : "") + "<color=" + (ok ? "#8FE08A" : "#FF8A70") + ">" + n + " " + Items.Get(t).Name.ToLowerInvariant() + "</color>";
                }
            string state = bs.CanPlace ? "<color=#8FE08A>Clique para construir</color>" : "<color=#FF8A70>" + (bs.Reason ?? "") + "</color>";
            string dismantle = bs.Aimed != null ? "   <color=#FFD27A>X</color> desmontar " + Building.Name(bs.Aimed.Kind).ToLowerInvariant() : "";
            buildHintText.text = "<color=#FFD27A>" + Building.Name(bs.Selected).ToUpperInvariant() + "</color>   " + cost + "\n" + state +
                "   <color=#FFD27A>R</color> girar   <color=#FFD27A>Botão direito</color> trocar peça" + dismantle;
        }

        void RefreshChest()
        {
            if (openChest == null) { SetPanel(Panel.None); return; }
            if (game.Player != null && (openChest.transform.position - game.Player.transform.position).sqrMagnitude > 49f) { SetPanel(Panel.None); return; }
            int stamp = invVersion * 31 + openChest.Storage.Version;
            if (stamp == chestVersion) return;
            chestVersion = stamp;
            FillGrid(chestGrid, openChest.Storage, true);
            FillGrid(chestBagGrid, game.Inventory, false);
        }

        void FillGrid(RectTransform grid, Inventory inv, bool fromChest)
        {
            UIKit.ClearChildren(grid);
            const int cols = 6;
            const float size = 86, gap = 8;
            for (int i = 0; i < inv.Size; i++)
            {
                var st = inv.Slots[i];
                var img = UIKit.Img(grid, "Espaco", UIKit.Slot, null, true, 1f);
                img.raycastTarget = true;
                img.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2((i % cols) * (size + gap), -(i / cols) * (size + gap)), new Vector2(size, size));
                if (st.IsEmpty) continue;
                var ic = UIKit.Icon(img.transform, "Icone");
                ic.rectTransform.Fill(11, 11, 11, 11);
                ic.texture = Icons.Get(st.Type);
                if (st.Count > 1)
                    UIKit.Txt(img.transform, "Qtd", st.Count.ToString(), UIKit.BodyBold, 19, Color.white, TextAnchor.LowerRight).rectTransform.Fill(4, 4, 8, 4);
                int idx = i;
                var b = img.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() =>
                {
                    if (openChest == null) return;
                    if (fromChest) NetGame.ChestTake(openChest, idx); else NetGame.ChestPut(openChest, idx);
                    Sfx.UI(Sfx.Pickup, 0.4f);
                    chestVersion = -1;
                });
            }
        }

        void MoveStack(Inventory from, Inventory to, int idx)
        {
            var st = from.Slots[idx];
            if (st.IsEmpty) return;
            int left = to.Add(st.Type, st.Count, false);
            int moved = st.Count - left;
            if (moved <= 0) { Sfx.UI(Sfx.Error, 0.4f); Toast("Não cabe mais nada ali.", Bad); return; }
            from.TakeFromSlot(idx, moved);
            Sfx.UI(Sfx.Pickup, 0.4f);
            chestVersion = -1;
        }
    }
}
