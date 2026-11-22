using UnityEngine;

namespace MarAberto
{
    /// <summary>Algo que flutua e pode ser pilotado (bote antigo ou jangada montada pelo jogador).</summary>
    public abstract class Vessel : MonoBehaviour
    {
        public enum AnchorState { Up, Dropping, Set, Hanging, Raising }
        public string BoatName = "Barco";
        public float Throttle { get; set; }
        public float Steer { get; set; }
        public bool Driven { get; set; }
        public Transform Seat { get; protected set; }
        public float Speed { get; protected set; }
        public AnchorState Anchor { get; protected set; } = AnchorState.Up;
        public float AnchorDepth { get; protected set; }
        public abstract void ToggleAnchor();
        /// <summary>Linha extra no painel de pilotagem (ex.: combustível).</summary>
        public virtual string Extra => null;
        public virtual bool HasAnchor => true;
    }
}
