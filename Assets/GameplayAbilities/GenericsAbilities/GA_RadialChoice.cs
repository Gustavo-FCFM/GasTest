using System.Collections.Generic;
using UnityEngine;

// ============================================================
// GA_RadialChoice  (genérico — elegir un estado en una rueda, 21 de octubre de 2026)
//
// Una rueda (IRadialMenuAbility, como los tótems o las trampas) donde cada opción deja PUESTO un
// estado: un efecto con su tag, que otras habilidades miran. El libro de hechizos del Mago: elegir
// "Bola de fuego" le pone GE_Spell_Fire, y su clic izquierdo (un GA_TagSwitch) dispara la bola
// mientras lo tenga. Le va a servir también al Filo danzante (sus espadas).
//
//   · Al elegir una opción se le saca al dueño todo lo que dé el "Tag de lo elegido" (la opción
//     anterior) y se le pone el efecto de la nueva. Una opción SIN efecto solo saca: la de
//     arriba del libro, "Misiles arcanos", vuelve a lo normal.
//   · "Disponible si alcanza para": la opción se ve apagada (y no se elige) si no alcanza para
//     ese costo (el maná del hechizo). No lo cobra: lo cobra la habilidad que lo lanza.
//   · Elegir la que ya está no hace nada ni cobra.
//   · El botón muestra el ícono de la opción elegida.
//
// Las opciones van en orden: la primera arriba y después en sentido horario.
// ============================================================
[CreateAssetMenu(fileName = "GA_RadialChoice", menuName = "GAS/Generics/Radial Choice")]
public class GA_RadialChoice : GameplayAbility, IRadialMenuAbility
{
    [System.Serializable]
    public struct ChoiceOption
    {
        [Tooltip("Nombre en la rueda, en inglés.")]
        [Label("Nombre (inglés)")]
        public string Name;

        [Tooltip("Nombre en la rueda, en español. Vacío = sale el inglés.")]
        [Label("Nombre (español)")]
        public string NameEs;

        [Tooltip("Qué hace, debajo del nombre en la rueda, en inglés.")]
        [Label("Descripción (inglés)")]
        public string Description;

        [Tooltip("Qué hace, en español. Vacío = sale el inglés.")]
        [Label("Descripción (español)")]
        public string DescriptionEs;

        [Label("Ícono")]
        public Sprite Icon;

        [Tooltip("El estado que deja puesto: un efecto con duración que da el 'Tag de lo elegido' " +
                 "y el suyo propio (GE_Spell_Fire: Status_SpellSelected + Status_SpellFire). " +
                 "Vacío = esta opción solo saca lo elegido (vuelve a lo normal).")]
        [Label("Efecto al elegirla")]
        public GameplayEffect Effect;

        [Tooltip("La opción se ve apagada y no se puede elegir si el dueño no puede pagar este " +
                 "costo (el maná del hechizo). NO se cobra al elegirla. Vacío = siempre disponible.")]
        [Label("Disponible si alcanza para (GE)")]
        public GameplayEffect RequiresAffordable;
    }

    [Section("Opciones")]
    [Tooltip("Las opciones de la rueda, en orden: la primera arriba, después en sentido horario.")]
    [Label("Opciones")]
    public List<ChoiceOption> Options = new List<ChoiceOption>();

    [Tooltip("El tag que dan TODOS los estados de esta rueda (Status_SpellSelected). Al elegir otra " +
             "opción se saca todo lo que lo dé: así nunca quedan dos puestos.")]
    [Label("Tag de lo elegido")]
    public EGameplayTag ChoiceTag = EGameplayTag.None;

    // IRadialMenuAbility: la rueda lee arrays, se arman de la lista.
    public float    MaxRadialRange     => 10f;
    public Sprite[] RadialIcons        => Collect(o => o.Icon);
    public string[] RadialLabels       => Collect(o => Loc.Pick(o.Name, o.NameEs));
    public string[] RadialDescriptions => Collect(o => Loc.Pick(o.Description, o.DescriptionEs));

    private T[] Collect<T>(System.Func<ChoiceOption, T> pick)
    {
        if (Options == null) return new T[0];
        var result = new T[Options.Count];
        for (int i = 0; i < Options.Count; i++) result[i] = pick(Options[i]);
        return result;
    }

    public bool IsRadialOptionAvailable(int index)
    {
        if (Options == null || index < 0 || index >= Options.Count || OwnerASC == null) return false;
        GameplayEffect cost = Options[index].RequiresAffordable;
        return cost == null || OwnerASC.CanAffordGameplayEffect(cost);
    }

    // La opción puesta ahora: la primera cuyo estado (sus tags, menos el común) tenga el dueño;
    // si no tiene ninguno, la primera sin efecto (lo normal). -1 si no hay. Corre en el dueño
    // (los tags le llegan sincronizados) y en el servidor.
    public int CurrentIndex
    {
        get
        {
            if (Options == null || OwnerASC == null) return -1;
            for (int i = 0; i < Options.Count; i++)
                if (Options[i].Effect != null && HasState(Options[i].Effect)) return i;
            for (int i = 0; i < Options.Count; i++)
                if (Options[i].Effect == null) return i;
            return -1;
        }
    }

    private bool HasState(GameplayEffect effect)
    {
        bool any = false;
        foreach (EGameplayTag tag in effect.GrantedTags)
        {
            if (tag == EGameplayTag.None || tag == ChoiceTag) continue;
            if (!OwnerASC.HasTag(tag)) return false;
            any = true;
        }
        return any;
    }

    // El botón muestra la opción puesta.
    public override Sprite CurrentIcon
    {
        get
        {
            int current = CurrentIndex;
            return current >= 0 && Options[current].Icon != null ? Options[current].Icon : AbilityIcon;
        }
    }

    public override bool UsesTargetLayer => false;
    public override bool UsesHitEffects  => false;

    // Se activa por la rueda (ActivateWithSelection), no con un click.
    public override void Activate() { }

    public void ActivateWithSelection(int index, Vector3 targetPosition)
    {
        if (!IsServer) return;
        if (Options == null || index < 0 || index >= Options.Count || !CanActivate() ||
            !IsRadialOptionAvailable(index) || index == CurrentIndex)
        {
            EndAbility();
            return;
        }

        if (ChoiceTag != EGameplayTag.None) OwnerASC.RemoveEffectsWithTag(ChoiceTag);

        GameplayEffect effect = Options[index].Effect;
        if (effect != null) OwnerASC.ApplyGameplayEffect(effect, this);

        CommitAbility();
        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null) pc.PlayAnimation(this);
        EndAbility();
    }
}
