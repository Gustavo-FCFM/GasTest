using UnityEngine;

// ============================================================
// TargetImpactAbility
//
// Base de las habilidades que dibujan un VFX de impacto sobre un PERSONAJE: la
// curación apuntada, marcar a un enemigo, revivir a un aliado, la Intercepción heroica.
// Antes cada una tenía su propia copia de "ImpactVFX, instanciar, borrar a los 2 s";
// ahora lo tienen todas igual, con las mismas tres opciones en el Inspector.
//
// Cómo se usa desde una habilidad: PlayImpactVFXOnTarget(objetivo) en el servidor.
// Manda el OBJETIVO por red (NetworkASC.ServerPlayAbilityVFXOn), no una posición, así
// cada pantalla le pega el VFX a él y lo sigue si se mueve. Para un VFX en un punto
// del mundo (donde aterrizó alguien) sigue estando ServerPlayAbilityVFX con la
// posición, que acá usa la misma duración.
//
// Los campos se llaman igual que tenían en cada habilidad: los VFX ya cargados en los
// assets no se pierden.
// ============================================================
public abstract class TargetImpactAbility : GameplayAbility
{
    [Header("VFX de impacto")]
    [Tooltip("VFX que aparece sobre el personaje alcanzado (se ve en todas las pantallas).")]
    public GameObject ImpactVFX;

    [Tooltip("Pegar el VFX al personaje, así lo sigue si se mueve (y se va con él si " +
             "desaparece). Apagado, queda quieto donde apareció.")]
    public bool AttachImpactVFX = true;

    [Tooltip("Dónde aparece el VFX, medido desde los pies del personaje. (0, 1, 0) es a la " +
             "altura del pecho; un aura pensada para ir en el piso va con (0, 0, 0).")]
    public Vector3 ImpactVFXOffset = new Vector3(0f, 1f, 0f);

    [Tooltip("Segundos hasta que el VFX se borra.")]
    public float ImpactVFXLifetime = 2f;

    // Reproduce el VFX sobre el objetivo en todas las pantallas. Solo servidor.
    protected void PlayImpactVFXOnTarget(AbilitySystemComponent target)
    {
        if (target == null) return;

        NetworkAbilitySystemComponent netAsc = OwnerASC != null
            ? OwnerASC.GetComponent<NetworkAbilitySystemComponent>() : null;

        if (netAsc != null) netAsc.ServerPlayAbilityVFXOn(this, target);
        else                PlayImpactVFXOn(target);   // sin red (escena de pruebas suelta)
    }

    public override void PlayImpactVFX(Vector3 position)
    {
        if (ImpactVFX == null) return;
        Destroy(Instantiate(ImpactVFX, position, Quaternion.identity), ImpactVFXLifetime);
    }

    public override void PlayImpactVFXOn(AbilitySystemComponent target)
    {
        if (ImpactVFX == null || target == null) return;

        Transform t = target.transform;
        GameObject vfx = Instantiate(ImpactVFX, t.position + ImpactVFXOffset, Quaternion.identity);
        if (AttachImpactVFX) vfx.transform.SetParent(t, true);
        Destroy(vfx, ImpactVFXLifetime);
    }
}
