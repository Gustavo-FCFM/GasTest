using UnityEngine;
using System.Collections.Generic;

// ============================================================
// CharacterClassDefinition
//
// Asset que define una clase jugable completa: identidad visual,
// stats base, qué habilidad va en cada slot, cómo escala al subir
// de nivel, y a qué subclases puede evolucionar. PlayerController
// lo usa en EquipCharacterClass() para configurar todo el personaje
// de una sola vez.
// ============================================================
[CreateAssetMenu(fileName = "Class_New", menuName = "GAS/Character Class Definition")]
public class CharacterClassDefinition : ScriptableObject
{
    // Qué habilidad ocupa cada slot de input (Shift, Q, E, R, LMB, RMB).
    [System.Serializable]
    public struct AbilityAssignment
    {
        public EAbilityInput InputSlot;
        public GameplayAbility Ability;
    }

    [Section(ClassSection.Identity)]
    public string ClassName = "Aldeano";
    public Sprite ClassIcon;
    [TextArea] public string Description;

    [Tooltip("El rol en el equipo. Decide qué le carga la definitiva además de sus golpes: " +
             "Tank al aguantar daño, Damage al matar a un personaje enemigo, Support al " +
             "curar a un aliado. None para las clases BASE, que todavía no tienen definitiva.")]
    public EClassRole Role = EClassRole.None;

    [Tooltip("Bajo qué rol aparece en los menús (la sala y el menú de clases las agrupan por rol). " +
             "None = el suyo; una clase BASE, que no tiene rol, aparece con el de la mayoría de sus " +
             "subclases. Hace falta solo para una clase base que todavía no tiene subclases.")]
    public EClassRole MenuRole = EClassRole.None;

    // El rol con el que se la muestra en los menús. No cambia nada del juego (la carga de
    // la definitiva sigue mirando Role): solo dónde se la lista.
    public EClassRole DisplayRole
    {
        get
        {
            if (MenuRole != EClassRole.None) return MenuRole;
            if (Role != EClassRole.None) return Role;

            // Una clase base: el rol de la mayoría de sus subclases (en empate, el primero
            // del orden de los menús: Tanque, Daño, Soporte).
            if (AvailableSubclasses == null) return EClassRole.None;
            var counts = new int[4];
            foreach (CharacterClassDefinition sub in AvailableSubclasses)
                if (sub != null && sub != this && (int)sub.Role > 0 && (int)sub.Role < counts.Length)
                    counts[(int)sub.Role]++;

            EClassRole best = EClassRole.None;
            int bestCount = 0;
            foreach (EClassRole r in ClassRoleStyle.MenuOrder)
                if ((int)r > 0 && counts[(int)r] > bestCount) { best = r; bestCount = counts[(int)r]; }
            return best;
        }
    }

    [Section(ClassSection.Stats)]
    [Tooltip("Los stats con los que arranca (nivel 1): un ASDef_*.")]
    public AttributeSetDefinition BaseAttributes;

    [Section(ClassSection.Abilities)]
    [Tooltip("Qué habilidad va en cada botón (clic izq., clic der., Q, E, R, Shift, pasiva).")]
    public List<AbilityAssignment> Abilities;

    [Section(ClassSection.Passives)]
    [Tooltip("Prefab con los componentes de código propios de esta clase (ej. IllusoryBladesPassive " +
             "del Ilusionista, PlayerVisibility del Asesino). Se instancia como HIJO del jugador al " +
             "equipar la clase y se destruye al cambiarla, así el prefab del Player queda limpio y " +
             "cada clase trae solo lo suyo. Sus componentes llegan al ASC con GetComponentInParent. " +
             "Dejar None si la clase no necesita lógica en C#.")]
    public GameObject PassiveBehaviorsPrefab;

    [Tooltip("GameplayEffects que se aplican al equipar la clase y permanecen activos toda la partida. " +
             "Pensados para pasivas (ej: el tag de Ataque Furtivo del Pícaro). Deben tener una Duration " +
             "muy grande (casi infinita) y normalmente EffectType = Hidden para no ensuciar la barra de buffs. " +
             "Se aplican con autoridad de servidor y se resincronizan a los clientes por los canales normales.")]
    public List<GameplayEffect> PassiveEffects;

    [Section(ClassSection.Movement)]
    [Tooltip("Cómo se mueve en las paredes (WallMove_*): pegarse y saltar (Pícaro) o correr por " +
             "ellas (Monje). Las subclases apuntan al mismo perfil que su clase base. None = no se " +
             "engancha a las paredes.")]
    public WallMovementProfile WallMovement;

    [Section(ClassSection.Stats)]
    [Tooltip("Cuánto sube cada stat por cada nivel ganado.")]
    public List<AttributeGrowth> StatGrowthPerLevel;

    [Section(ClassSection.Evolution)]
    [Tooltip("A qué subclases puede evolucionar al llegar al nivel máximo.")]
    // A qué subclases se puede evolucionar al llegar al nivel máximo
    // (ver UI_LevelUpSelectionSystem).
    public List<CharacterClassDefinition> AvailableSubclasses;

    [Section(ClassSection.Animation)]
    [Tooltip("Override de animaciones de la clase (AOC_*): el idle, caminar, correr y los clips de base.")]
    // Override de animaciones específico de esta clase (idle agresivo,
    // ataque pesado vs rápido, etc.).
    public AnimatorOverrideController ClassAnimatorOverride;

    // Animaciones de una POSTURA: mientras el personaje tenga StanceAnimatorTag, los clips
    // de este override (quieto, caminar, correr...) reemplazan a los de la clase, y al
    // perder el tag vuelven. El Maestro de batalla lo usa en ofensiva: de espada y escudo
    // (AOC_Paladin_SwordAndShield) pasa al mandoble a dos manos (AOC_Paladin_2Handed).
    // Tiene que estar armado sobre el mismo Animator base que el de la clase. Las ranuras
    // de las habilidades (PLACEHOLDER_...) no se tocan.
    [Tooltip("Override que se aplica encima del de la clase mientras tenga el tag de abajo " +
             "(ej. el Maestro de batalla en ofensiva: AOC_Paladin_2Handed). Vacío = nunca cambia.")]
    public AnimatorOverrideController StanceAnimatorOverride;

    [ShowIf(nameof(StanceAnimatorOverride))]
    [Tooltip("Mientras tenga este tag se usa el override de arriba. None = nunca.")]
    public EGameplayTag StanceAnimatorTag = EGameplayTag.None;

    [Section(ClassSection.Weapons)]
    [Tooltip("Arma principal (mano derecha). Vacío = sin arma (el Monje).")]
    public GameObject MainHandWeaponPrefab;
    [Tooltip("Arma secundaria (mano izquierda), opcional: un escudo.")]
    public GameObject OffHandWeaponPrefab;

    // ============================================================
    // AJUSTE DE POSE DE LAS ARMAS
    //
    // El arma se instancia como hija del socket del hueso y su transform local se
    // fija desde acá. La rotación PROPIA del prefab se descarta a propósito: un
    // prefab de arma suele venir con la rotación con la que quedó guardado en la
    // escena de origen, que no significa nada respecto de la mano.
    //
    // POR QUÉ EL AJUSTE VA EN LA CLASE Y NO EN EL ARMA: los dos sockets no tienen la
    // misma orientación (Socket_OffHand lleva 180° de "roll" extra respecto de
    // Socket_MainHand), así que un mismo modelo necesita poses distintas según en qué
    // mano vaya. Además dos clases pueden usar el mismo modelo empuñado distinto.
    //
    // En CERO (por defecto) el arma queda exactamente pegada al hueso, que es como
    // se comportaba antes: las clases ya configuradas no cambian en nada.
    // ============================================================

    [ShowIf(nameof(MainHandWeaponPrefab))]
    [Tooltip("Rotación extra (grados) del arma principal respecto del hueso de la mano. " +
             "En cero queda pegada al hueso. Usalo si el modelo aparece torcido o al revés.")]
    public Vector3 MainHandRotationOffset;

    [ShowIf(nameof(MainHandWeaponPrefab))]
    [Tooltip("Desplazamiento extra del arma principal respecto del hueso, en espacio local.")]
    public Vector3 MainHandPositionOffset;

    [ShowIf(nameof(OffHandWeaponPrefab))]
    [Tooltip("Rotación extra (grados) del arma secundaria. Ojo: el socket de esta mano viene " +
             "con 180° de giro respecto del de la principal, así que un escudo suele necesitar " +
             "compensarlo acá (probá 180 en Z).")]
    public Vector3 OffHandRotationOffset;

    [ShowIf(nameof(OffHandWeaponPrefab))]
    [Tooltip("Desplazamiento extra del arma secundaria respecto del hueso, en espacio local.")]
    public Vector3 OffHandPositionOffset;

    // ============================================================
    // POSE DE LA MANO SECUNDARIA
    //
    // Una pose fija para el brazo IZQUIERDO mientras se juega la clase: el libro del
    // Clérigo (HumanM@ObjectBook01_L, en Kevin Iglesias > Masked Poses), un farol, un
    // escudo sostenido de otra forma... Va en la capa OffHandPose del Animator, que
    // tiene la máscara del brazo izquierdo: el resto del cuerpo corre, salta y ataca
    // igual que siempre, y ese brazo se queda sosteniendo el objeto.
    //
    // Vacío = la capa queda en peso 0 y el brazo se anima normal (todas las clases de
    // antes). Si el Animator no tiene la capa (falta correr la herramienta), no hace nada.
    // ============================================================

    [Section(ClassSection.Animation)]
    [Tooltip("Pose del brazo izquierdo mientras se juega la clase (ej. el libro del Clérigo: " +
             "HumanM@ObjectBook01_L). Vacío = el brazo se anima normal.")]
    public AnimationClip OffHandPose;

    [ShowIf(nameof(OffHandPose))]
    [Tooltip("La pose de arriba se aplica SOLO mientras el personaje tenga este tag. None = " +
             "siempre. El Maestro de batalla la usa con Stance_Offensive: en ofensiva toma el " +
             "mandoble a dos manos.")]
    public EGameplayTag OffHandPoseRequiredTag = EGameplayTag.None;

    // ============================================================
    // ARMA SECUNDARIA A LA ESPALDA
    //
    // Mientras el personaje tenga StowOffHandTag, el arma de la mano izquierda (el escudo
    // del Guerrero) se cuelga del PECHO (el hueso Chest del esqueleto humanoide, así no
    // hace falta crear un socket) y vuelve a la mano al perder el tag. Los tags viajan a
    // todos, así que se ve igual en todas las pantallas. Lo usan las subclases del
    // Guerrero en postura ofensiva: "dejo la defensa para enfocarme en atacar".
    // ============================================================

    [Section(ClassSection.Weapons)]
    [ShowIf(nameof(OffHandWeaponPrefab))]
    [Tooltip("Mientras tenga este tag, el arma secundaria va a la espalda. None = nunca.")]
    public EGameplayTag StowOffHandTag = EGameplayTag.None;

    [ShowIf(nameof(OffHandWeaponPrefab))]
    [ShowIf(nameof(StowOffHandTag), EGameplayTag.None, true)]
    [Tooltip("Posición del arma guardada, en el espacio del hueso del pecho. Se ajusta mirando " +
             "en Play (cambiar la clase y volver a equiparla la recoloca).")]
    public Vector3 StowedOffHandPositionOffset;

    [ShowIf(nameof(OffHandWeaponPrefab))]
    [ShowIf(nameof(StowOffHandTag), EGameplayTag.None, true)]
    [Tooltip("Rotación (grados) del arma guardada, en el espacio del hueso del pecho.")]
    public Vector3 StowedOffHandRotationOffset;

    [ShowIf(nameof(OffHandWeaponPrefab))]
    [Tooltip("Tamaño del arma secundaria respecto del prefab. 1 = como viene. El Guardián lleva " +
             "el escudo de siempre más grande (1.3) mientras no haya un modelo propio.")]
    public float OffHandScale = 1f;

    // ============================================================
    // CRECER (el Avatar del Guardián)
    //
    // Mientras el personaje tenga GrowTag, el MODELO crece a GrowScale (se ve en todas
    // las pantallas: los tags viajan solos). Solo el modelo: la cápsula que recibe los
    // golpes y la cámara no cambian. El alcance de los golpes lo sube aparte el atributo
    // MeleeRangeBonus.
    // ============================================================

    [Section(ClassSection.Grow, startCollapsed: true)]
    [Tooltip("Mientras tenga este tag, el modelo crece a Grow Scale. None = nunca.")]
    public EGameplayTag GrowTag = EGameplayTag.None;

    [ShowIf(nameof(GrowTag), EGameplayTag.None, true)]
    [Tooltip("Tamaño del modelo con el tag (1.5 = 50 % más grande).")]
    public float GrowScale = 1.5f;
}

// Cuánto sube UN atributo por cada nivel que gana el personaje. La lista
// StatGrowthPerLevel de arriba tiene una de estas por stat que quiera
// crecer con el nivel.
[System.Serializable]
public class AttributeGrowth
{
    public EAttributeType Attribute;
    public float AmountPerLevel; // Ej: +5 de Vida por nivel
}
