using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;

// ============================================================
// GA_ElementalFury
//
// Ultimate de Chamán: invoca hasta 4 tótems inmortales en las
// esquinas de un cuadrado alrededor del dueño, y además crea un
// tornado de daño continuo (con VFX) que dura Duration segundos.
// Al terminar, despawnea los tótems invocados.
// ============================================================
[CreateAssetMenu(fileName = "GA_ElementalFury", menuName = "GAS/Specific Abilities/Barbarian/Shaman/Elemental Fury")]
public class GA_ElementalFury : GameplayAbility
{
    [Section("Tótems")]
    [Tooltip("Hasta 4 prefabs de tótem, uno por esquina del cuadrado.")]
    public GameObject[] TotemPrefabs;
    [Tooltip("Distancia del centro a cada esquina donde aparecen los tótems.")]
    public float SquareRadius = 4f;

    [Section(AbilitySection.Shape)]
    [Tooltip("Radio del tornado: a quiénes alcanza cada tick. El VFX del tornado es una entrada " +
             "'En el impacto' (con 'Calzar con el área' mide lo mismo; con 'Lo sigue' sigue al " +
             "dueño si el tornado lo sigue).")]
    public float DamageRadius = 5f;
    [Tooltip("El tornado sigue al dueño. Apagado = queda fijo donde se activó.")]
    public bool FollowPlayer = false;

    [Section(AbilitySection.Timing)]
    [Tooltip("Cuánto dura el tornado (y por lo tanto, cuánto viven los tótems).")]
    public float Duration = 10f;
    [Tooltip("Cada cuánto se aplican los efectos a quien esté dentro del tornado.")]
    public float TickRate = 0.5f;

    public override float VisualAreaRadius => DamageRadius;
    protected override float VisualDefaultLifetime => Duration;
    public override bool SupportsVisualTiming(EVisualWhen when) => true;
    protected override Transform VisualImpactParent(AbilitySystemComponent owner)
        => FollowPlayer && owner != null ? owner.transform : null;

    // Valida, cobra costo/cooldown y arranca la secuencia de invocación +
    // tornado.
    public override void Activate()
    {
        if (!IsServer) return;
        if (!CanActivate()) return;

        CommitAbility();

        if (OwnerASC != null)
        {
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null) pc.PlayAnimation(this);
            OwnerASC.StartAbilityCoroutine(ElementalFuryRoutine());
        }
        else
        {
            EndAbility();
        }
    }

    // Invoca los tótems en red en las esquinas del cuadrado, reproduce el
    // VFX del tornado, aplica daño periódico a los enemigos dentro de
    // DamageRadius durante Duration segundos, y al terminar despawnea los
    // tótems invocados.
    private IEnumerator ElementalFuryRoutine()
    {
        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        Vector3 centerPos = OwnerASC.transform.position;

        yield return new WaitForSeconds(0.5f);

        if (pc != null) pc.FinishAttack();

        // Crear tótems en esquinas del cuadrado
        List<GameObject> ultimateTotems = new List<GameObject>();
        Vector3[] corners = new Vector3[]
        {
            new Vector3( 1, 0,  1).normalized * SquareRadius,
            new Vector3( 1, 0, -1).normalized * SquareRadius,
            new Vector3(-1, 0,  1).normalized * SquareRadius,
            new Vector3(-1, 0, -1).normalized * SquareRadius
        };

        for (int i = 0; i < TotemPrefabs.Length && i < 4; i++)
        {
            if (TotemPrefabs[i] == null) continue;

            Vector3 spawnPos = centerPos + corners[i];
            if (Physics.Raycast(spawnPos + Vector3.up * 5f, Vector3.down, out RaycastHit hit, 15f))
                spawnPos = hit.point;

            GameObject totemObj = Instantiate(TotemPrefabs[i], spawnPos, Quaternion.identity);

            Entity_Totem totemScript = totemObj.GetComponent<Entity_Totem>();
            if (totemScript != null)
            {
                totemScript.MyTeamID   = OwnerASC.TeamID;
                totemScript.CreatorASC = OwnerASC;
            }

            // Indestructibles, como pide el diseño. Status_Immunity es la inmunidad
            // REAL (el daño ni entra, la barra se queda llena); Status_Immortal solo
            // impide bajar de 1 de vida, y con eso la barra caía a casi nada y el tótem
            // se veía roto sin romperse. Se dejan los dos: el segundo es la red por si
            // algún daño llegara a saltearse la inmunidad.
            AbilitySystemComponent totemASC = totemObj.GetComponent<AbilitySystemComponent>();
            if (totemASC != null)
            {
                totemASC.AddTag(EGameplayTag.Status_Immunity);
                totemASC.AddTag(EGameplayTag.Status_Immortal);
            }

            // Instantiate() normal solo crea el tótem en el servidor — sin
            // esto, es invisible para cualquier cliente que no sea el host.
            NetworkObject totemNob = totemObj.GetComponent<NetworkObject>();
            if (totemNob != null)
                InstanceFinder.ServerManager.Spawn(totemNob);
            else
                Debug.LogWarning("[GA_ElementalFury] El TotemPrefab no tiene NetworkObject — no se va a replicar a los clientes.");

            ultimateTotems.Add(totemObj);
        }

        // Instanciar tornado visual. Instantiate() acá solo se vería en el
        // proceso servidor — BroadcastImpactVFX lo reproduce en todos los
        // peers (cada uno con su propia copia, que se autodestruye sola
        // tras Duration en vez de que la sigamos con una referencia acá).
        BroadcastImpactVFX(centerPos);

        // Bucle de daño. Un tick de 0 no puede ser "cada frame" (el tiempo no avanzaría
        // nunca): como mínimo, 20 por segundo.
        float tick = Mathf.Max(0.05f, TickRate);
        float timeElapsed = 0f;
        bool firstEnemy = true;
        while (timeElapsed < Duration)
        {
            Vector3 currentCenter = FollowPlayer ? OwnerASC.transform.position : centerPos;
            Collider[] hits = Physics.OverlapSphere(currentCenter, DamageRadius, TargetLayer);
            foreach (var hitCol in hits)
            {
                AbilitySystemComponent targetASC = hitCol.GetComponentInParent<AbilitySystemComponent>();
                if (targetASC == null) continue;

                bool enemy = IsEnemy(targetASC);
                if (!ApplyHitEffects(targetASC, firstHit: enemy && firstEnemy)) continue;
                if (enemy) firstEnemy = false;
            }

            yield return new WaitForSeconds(tick);
            timeElapsed += tick;
        }

        // Limpieza
        foreach (var t in ultimateTotems)
        {
            if (t == null) continue;
            NetworkObject nob = t.GetComponent<NetworkObject>();
            if (nob != null && nob.IsSpawned) InstanceFinder.ServerManager.Despawn(nob);
            else Destroy(t);
        }

        EndAbility();
    }

    // El tornado y las cuatro esquinas de los tótems.
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.25f);
        Gizmos.DrawSphere(origin.position, DamageRadius);

        Gizmos.color = new Color(0.4f, 1f, 0.5f, 0.9f);
        foreach (Vector3 corner in TotemCorners())
            Gizmos.DrawWireCube(origin.position + corner + Vector3.up * 0.75f, new Vector3(0.4f, 1.5f, 0.4f));
    }

    private Vector3[] TotemCorners() => new Vector3[]
    {
        new Vector3( 1, 0,  1).normalized * SquareRadius,
        new Vector3( 1, 0, -1).normalized * SquareRadius,
        new Vector3(-1, 0,  1).normalized * SquareRadius,
        new Vector3(-1, 0, -1).normalized * SquareRadius,
    };

#if UNITY_EDITOR
    public override void DrawSceneHandles(Transform origin)
    {
        if (origin == null) return;

        AbilityHandles.Radius(this, "Damage Radius", origin.position, ref DamageRadius,
                              AbilityHandles.RadiusColor, origin.right);
        AbilityHandles.Distance(this, "Square Radius", origin.position, new Vector3(1, 0, 1).normalized,
                                ref SquareRadius, AbilityHandles.DistanceColor);
    }
#endif

    // =========================================================
    // DATOS VIEJOS (solo para pasarlos a las listas; ver GameplayAbility.UpgradeLegacyData)
    // =========================================================

    [SerializeField, HideInInspector] private GameplayEffect DamageEffect;
    [SerializeField, HideInInspector] private GameObject TornadoVFXPrefab;
    [SerializeField, HideInInspector] private float TornadoVfxScaleMultiplier = 2f;

    protected override void OnUpgradeLegacyData(ref bool changed)
    {
        base.OnUpgradeLegacyData(ref changed);

        UpgradeEffect(ref DamageEffect, EEffectWhen.OnHit, EEffectTarget.Enemies, ref changed);
        UpgradeVisual(ref TornadoVFXPrefab, new AbilityVisual
        {
            When = EVisualWhen.OnImpact, MatchAreaSize = true, Attach = true,
            AreaSizeMultiplier = TornadoVfxScaleMultiplier,
        }, ref changed);
    }
}
