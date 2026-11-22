using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace MarAberto
{
    /// <summary>Aba FABRICAR do Tab: categorias, receitas, pesquisa e fabricação.</summary>
    public partial class Hud
    {
        RectTransform craftView, craftCats, craftGrid, craftDetail;
        CraftCategory craftCat = CraftCategory.Ferramentas;
        Recipe craftSel;
        int craftVersion = -1, craftDirty;
        readonly List<(Text label, Image bg, CraftCategory cat)> catButtons = new List<(Text, Image, CraftCategory)>();

        void BuildCraft(RectTransform body)
        {
            craftView = UIKit.Rect(body, "VistaFabricar").Fill();
            craftCats = UIKit.Rect(craftView, "Categorias").Place(new Vector2(0, 1), new Vector2(0, 1), Vector2.zero, new Vector2(240, 560));
            int i = 0;
            foreach (CraftCategory c in System.Enum.GetValues(typeof(CraftCategory)))
            {
                var cat = c;
                var bg = UIKit.Img(craftCats, "Cat" + c, UIKit.Slot, new Color(1, 1, 1, 0.5f), true, 0.6f);
                bg.raycastTarget = true;
                bg.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, -i * 60), new Vector2(230, 54));
                var b = bg.gameObject.AddComponent<Button>();
                b.transition = Selectable.Transition.None;
                b.onClick.AddListener(() => { craftCat = cat; craftSel = null; Sfx.UI(Sfx.Page, 0.4f); craftDirty++; });
                var t = UIKit.Txt(bg.transform, "Texto", Crafting.CategoryName(c).ToUpperInvariant(), UIKit.Title, 18, UIKit.Muted, TextAnchor.MiddleLeft);
                t.rectTransform.Fill(18, 0, 10, 0);
                catButtons.Add((t, bg, c));
                i++;
            }
            UIKit.Img(craftView, "Fio1", UIKit.White, new Color(UIKit.Brass.r, UIKit.Brass.g, UIKit.Brass.b, 0.3f)).rectTransform
                .Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(252, 0), new Vector2(1, 540));
            craftGrid = UIKit.Rect(craftView, "Receitas").Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(270, 0), new Vector2(650, 560));
            UIKit.Img(craftView, "Fio2", UIKit.White, new Color(UIKit.Brass.r, UIKit.Brass.g, UIKit.Brass.b, 0.3f)).rectTransform
                .Place(new Vector2(1, 1), new Vector2(1, 1), new Vector2(-410, 0), new Vector2(1, 540));
            craftDetail = UIKit.Rect(craftView, "Detalhe").Place(new Vector2(1, 1), new Vector2(1, 1), Vector2.zero, new Vector2(395, 560));
            Crafting.OnChanged += () => craftDirty++;
        }

        /// <summary>Abre o Tab já na aba FABRICAR (usado nos prints).</summary>
        public void PreviewCraft(CraftCategory cat)
        {
            SetPanel(Panel.Backpack, false);
            tab = Tab.Craft;
            craftCat = cat;
            craftSel = null;
            RefreshTabs();
            RefreshCraft();
        }

        int CraftStamp() => craftDirty * 7919 + invVersion * 31 + (int)craftCat + (craftSel != null ? craftSel.Id.GetHashCode() : 0);

        void RefreshCraft()
        {
            int stamp = CraftStamp();
            if (stamp == craftVersion) return;
            craftVersion = stamp;
            var inv = game.Inventory;

            foreach (var (label, bg, cat) in catButtons)
            {
                bool on = cat == craftCat;
                int ready = 0;
                foreach (var r in Crafting.All)
                    if (r.Category == cat && (Crafting.CanCraft(inv, r, out _) || Crafting.CanResearch(inv, r))) ready++;
                label.text = Crafting.CategoryName(cat).ToUpperInvariant() + (ready > 0 ? "  <color=#8FE08A>•</color>" : "");
                label.color = on ? UIKit.Gold : UIKit.Muted;
                bg.color = on ? new Color(1f, 0.85f, 0.5f, 0.9f) : new Color(1, 1, 1, 0.45f);
            }

            // Grade de receitas da categoria
            UIKit.ClearChildren(craftGrid);
            var list = new List<Recipe>();
            foreach (var r in Crafting.All) if (r.Category == craftCat) list.Add(r);
            if (craftSel == null || craftSel.Category != craftCat) craftSel = list.Count > 0 ? list[0] : null;
            const int cols = 5;
            const float w = 122, h = 150, gap = 10;
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                bool coming = Crafting.IsComing(r), unlocked = Crafting.IsUnlocked(r);
                bool can = Crafting.CanCraft(inv, r, out _), canRes = Crafting.CanResearch(inv, r);
                var card = UIKit.Img(craftGrid, "Receita", r == craftSel ? UIKit.SlotSelected : UIKit.Slot, null, true, 1f);
                card.raycastTarget = true;
                card.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2((i % cols) * (w + gap), -(i / cols) * (h + gap)), new Vector2(w, h));
                if (coming) card.color = new Color(1, 1, 1, 0.45f);
                var btn = card.gameObject.AddComponent<Button>();
                btn.transition = Selectable.Transition.None;
                var rr = r;
                btn.onClick.AddListener(() => { craftSel = rr; Sfx.UI(Sfx.Page, 0.3f); craftDirty++; });
                var ic = UIKit.Icon(card.transform, "Icone");
                ic.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(84, 84));
                ic.texture = r.IconItem != ItemType.Nada ? Icons.Get(r.IconItem) : null;
                ic.enabled = ic.texture != null;
                ic.color = coming ? new Color(0.4f, 0.4f, 0.4f, 0.6f) : unlocked ? Color.white : new Color(0.55f, 0.55f, 0.6f, 0.9f);
                var nm = UIKit.Txt(card.transform, "Nome", r.Name, UIKit.BodyBold, 15, coming ? UIKit.Muted : UIKit.Text, TextAnchor.UpperCenter);
                nm.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 22), new Vector2(w - 8, 38));
                string tag = coming ? "EM BREVE" : !unlocked ? (canRes ? "PESQUISAR" : "BLOQUEADA") : can ? "FABRICAR" : "";
                Color tc = coming ? UIKit.Muted : !unlocked ? (canRes ? UIKit.Gold : UIKit.BadC) : UIKit.GoodC;
                var tg = UIKit.Txt(card.transform, "Estado", tag, UIKit.Title, 12, tc, TextAnchor.LowerCenter);
                tg.rectTransform.Place(new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 6), new Vector2(w, 18));
            }

            // Detalhe da receita
            UIKit.ClearChildren(craftDetail);
            if (craftSel == null) return;
            var sel = craftSel;
            bool sComing = Crafting.IsComing(sel), sUnlocked = Crafting.IsUnlocked(sel);
            var big = UIKit.Img(craftDetail, "Quadro", UIKit.Slot, null, true, 1f);
            big.rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, 0), new Vector2(120, 120));
            var bic = UIKit.Icon(big.transform, "Icone");
            bic.rectTransform.Fill(12, 12, 12, 12);
            bic.texture = sel.IconItem != ItemType.Nada ? Icons.Get(sel.IconItem) : null;
            bic.enabled = bic.texture != null;
            UIKit.Txt(craftDetail, "Nome", sel.Name, UIKit.Title, 26, UIKit.Cream, TextAnchor.UpperLeft)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(156, -6), new Vector2(240, 70));
            UIKit.Txt(craftDetail, "Cat", Crafting.CategoryName(sel.Category).ToUpperInvariant(), UIKit.BodyBold, 14, UIKit.Label, TextAnchor.UpperLeft)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(156, -86), new Vector2(240, 22));
            UIKit.Txt(craftDetail, "Desc", sel.Description, UIKit.Body, 19, UIKit.Text, TextAnchor.UpperLeft)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, -136), new Vector2(370, 80));

            float y = -224;
            if (sComing)
            {
                UIKit.Txt(craftDetail, "EmBreve", "Chega na " + sel.ComingIn + " do desenvolvimento.", UIKit.BodyBold, 19, UIKit.Muted, TextAnchor.UpperLeft)
                    .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, y), new Vector2(370, 60));
                return;
            }
            if (!sUnlocked)
            {
                y = CostList(craftDetail, "PESQUISA  (uma vez, libera para sempre)", sel.Research, y);
                var rb = UIKit.Btn(craftDetail, "Pesquisar", "PESQUISAR", () =>
                {
                    if (Crafting.DoResearch(game.Inventory, sel)) { Sfx.UI(Sfx.Coin, 0.6f); Toast("Receita aprendida: " + sel.Name, Good); }
                    else Sfx.UI(Sfx.Error, 0.4f);
                }, 22);
                rb.interactable = Crafting.CanResearch(inv, sel);
                ((RectTransform)rb.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, y - 10), new Vector2(360, 54));
                y -= 80;
            }
            y = CostList(craftDetail, "FABRICAR", sel.Cost, y);
            bool ok = Crafting.CanCraft(inv, sel, out string why);
            var cb = UIKit.Btn(craftDetail, "Fabricar", ok ? "FABRICAR" : (why ?? "").ToUpperInvariant(), () =>
            {
                if (Crafting.DoCraft(game.Inventory, sel)) Sfx.UI(Sfx.Pickup, 0.6f);
                else Sfx.UI(Sfx.Error, 0.4f);
            }, 20);
            cb.interactable = ok;
            ((RectTransform)cb.transform).Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, y - 10), new Vector2(360, 54));
        }

        float CostList(RectTransform parent, string title, (ItemType type, int count)[] cost, float y)
        {
            UIKit.Txt(parent, "Titulo", title, UIKit.Title, 15, UIKit.Gold, TextAnchor.UpperLeft)
                .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, y), new Vector2(370, 22));
            y -= 28;
            var inv = game.Inventory;
            foreach (var (t, n) in cost)
            {
                int have = inv.Count(t);
                var row = UIKit.Rect(parent, "Linha").Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, y), new Vector2(370, 36));
                var ic = UIKit.Icon(row, "Icone");
                ic.rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), Vector2.zero, new Vector2(34, 34));
                ic.texture = Icons.Get(t);
                UIKit.Txt(row, "Nome", Items.Get(t).Name, UIKit.Body, 19, UIKit.Text, TextAnchor.MiddleLeft)
                    .rectTransform.Place(new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(44, 0), new Vector2(220, 30));
                UIKit.Txt(row, "Qtd", have + " / " + n, UIKit.BodyBold, 19, have >= n ? UIKit.GoodC : UIKit.BadC, TextAnchor.MiddleRight)
                    .rectTransform.Place(new Vector2(1, 0.5f), new Vector2(1, 0.5f), Vector2.zero, new Vector2(110, 30));
                y -= 38;
            }
            if (cost.Length == 0)
            {
                UIKit.Txt(parent, "Nada", "Grátis", UIKit.Body, 18, UIKit.Muted, TextAnchor.UpperLeft)
                    .rectTransform.Place(new Vector2(0, 1), new Vector2(0, 1), new Vector2(20, y), new Vector2(370, 26));
                y -= 30;
            }
            return y - 6;
        }
    }
}
