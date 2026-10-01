using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using FishNet;
using FishNet.Object;

// ============================================================
// GA_ProjectileShoot
//
// Dispara un proyectil físico (GC_Projectile) hacia el punto de mira
// del dueño. Esta habilidad solo se encarga de crear/lanzar el
// proyectil — toda la lógica de impacto (daño, VFX) vive en
// GC_Projectile, que recibe los datos necesarios en Initialize().
//
// APUNTAR ANTES DE LANZAR (casilla AimBeforeThrow, para los lanzamientos de arma):
// apretar levanta el arma y la deja atrás (AimHoldClip), la cámara del dueño se acerca a
// la mira y el modelo se le esconde (ThirdPersonOrbitCam); soltar lanza YA: se reproduce
// el lanzamiento desde esa pose (AimReleaseClip, sin el impulso hacia atrás) y el
// proyectil sale en el momento en que el clip suelta. Es un mantenido (IHoldAbility)
// SOLO con la casilla puesta: los demás disparos (castigos, el arco del combo del
// Clérigo) siguen saliendo al apretar.
// ============================================================
[CreateAssetMenu(fileName = "GA_ProjectileShoot", menuName = "GAS/Generics/Projectile Shoot")]
public class GA_ProjectileShoot : GameplayAbility, IHoldAbility
{
    [Header("Configuración del Proyectil")]
    public GameObject ProjectilePrefab;
    // Velocidad de salida del proyectil.
    public float      LaunchForce  = 20f;
    // Punto de salida, en espacio local del dueño (ej: la mano).
    public Vector3    SpawnOffset  = new Vector3(0.5f, 1.5f, 1.0f);
    // Si el proyectil gira sobre sí mismo mientras vuela (solo estético).
    public bool       AddSpin      = true;

    [Tooltip("Esconde el arma que el personaje tiene en la mano mientras el proyectil vuela.\n\n" +
             "Hace falta en los LANZAMIENTOS: el rig de Kevin Iglesias anima el hueso de prop " +
             "(B-handProp) como si el arma se soltara, asi que sin esto se ve volar el arma real " +
             "del jugador ADEMAS del proyectil. Se apaga al soltar y se vuelve a prender al " +
             "terminar la habilidad.\n\n" +
             "Apagado (lo normal) para magos y cualquier disparo que no suelte el arma.")]
    public bool       HideWeaponWhileFlying = false;

    [Header("Efectos al Impactar")]
    // Daño instantáneo al golpear.
    public GameplayEffect InstantDamageEffect;
    // Efecto con duración que además se le aplica al golpear (ej: veneno).
    public GameplayEffect DurationEffect;
    // Efectos EXTRA que se aplican al golpear, además de los dos anteriores. Opcional.
    public List<GameplayEffect> AdditionalEffects;

    [Tooltip("Efectos que esta habilidad le aplica a los ALIADOS que alcance. El daño y los " +
             "AdditionalEffects van a los enemigos; esta lista, a los aliados.\n\n" +
             "VACÍA (lo normal) = la habilidad ignora por completo a los aliados. En cuanto tenga " +
             "algo, empieza a considerarlos objetivos válidos: es lo que convierte un ataque normal " +
             "en uno que daña enemigos Y cura aliados a su paso (Castigo divino del Paladín).")]
    // FormerlySerializedAs: se llamó "AllyEffects" y vivía en GameplayAbility. Unity
    // serializa por NOMBRE, así que mientras el campo siga llamándose TargetEffects los
    // assets ya configurados conservan su valor al bajarlo a esta clase.
    [UnityEngine.Serialization.FormerlySerializedAs("AllyEffects")]
    public List<GameplayEffect> TargetEffects;

    [Tooltip("Efectos SOLO para el PRIMER enemigo que toca cada proyectil (el aturdido del " +
             "Clérigo del Orden). Los que atraviese después reciben el daño normal, no esto. " +
             "Vacío = nada.")]
    public List<GameplayEffect> FirstHitEffects;

    [Tooltip("Segundos mínimos antes de volver a aplicarle los FirstHitEffects al MISMO " +
             "enemigo. Sin esto, un aturdido de 1 s con ataques cada 0.8 s lo deja aturdido " +
             "para siempre. 0 = sin límite.")]
    public float FirstHitCooldownPerTarget = 3f;

    [Tooltip("Alcance del proyectil expresado en SEGUNDOS de vuelo (con velocidad constante, " +
             "alcance = LifeTime × LaunchForce). Pasado ese tiempo se despawnea solo aunque no haya " +
             "chocado con nada. 0 = usar el default del prefab (5s).\n\n" +
             "Para una estela/rayo que avanza lento y no cae, ponele al Rigidbody del prefab " +
             "Use Gravity en false y bajá LaunchForce.")]
    public float LifeTime = 0f;

    [Header("Sincronización")]
    // Espera entre activar la habilidad y que el proyectil realmente
    // salga (para sincronizar con la animación de disparo).
    public float SpawnDelay = 0.4f;

    [Tooltip("Distancia mínima a la que se considera que la retícula y el proyectil se " +
             "juntan. La retícula sale de la cámara y el proyectil de la mano: apuntando " +
             "a algo muy cerca, esas dos líneas se abren y el tiro sale visiblemente " +
             "torcido. Con esto se apunta a un punto del MISMO rayo pero a esta " +
             "distancia, así el tiro sale derecho y igual pasa por donde apuntaste. " +
             "Subirlo endereza más el tiro de cerca; bajarlo lo hace más literal.")]
    public float MinConvergeDistance = 4f;

    [Header("Visuales")]
    public GameObject ImpactVFX;

    [Header("Apuntar (mantener para apuntar, soltar para lanzar)")]
    [Tooltip("Mantener el botón apunta (el arma atrás, la cámara cerca de la mira, el modelo " +
             "oculto para el que apunta) y SOLTAR lanza al instante. Apagado = sale al apretar, " +
             "como siempre (los castigos, el arco del combo del Clérigo).")]
    public bool AimBeforeThrow = false;

    [Tooltip("Pose de apuntar, en bucle, mientras se mantiene (ej. HumanM@ThrowWeapon01_R - Hold).")]
    public AnimationClip AimHoldClip;

    [Tooltip("El lanzamiento DESDE la pose de apuntar, sin el impulso hacia atrás: se reproduce " +
             "al soltar. Si trae el evento AnimationEvent_HitFrame, el proyectil sale ahí. " +
             "Vacío = se usa el AnimationClip entero.")]
    public AnimationClip AimReleaseClip;

    [Tooltip("Si el clip de soltar no trae el evento: segundos desde que se suelta hasta que " +
             "sale el proyectil.")]
    public float AimReleaseDelay = 0.1f;

    [Tooltip("Corte de seguridad: si el aviso de soltar nunca llega, lanza a los tantos segundos.")]
    public float AimSafetyTimeout = 30f;

    [System.NonSerialized] private bool _aiming;
    [System.NonSerialized] private float _aimStartedAt;

    // IHoldAbility: solo con la casilla puesta se maneja como mantenido.
    public bool UsesHoldInput => AimBeforeThrow;
    public bool IsHolding     => _aiming;
    public AnimationClip HoldLoopClip   => AimBeforeThrow ? AimHoldClip : null;
    public AnimationClip HoldStartClip  => null;
    public AnimationClip HoldEndClip    => null;   // al soltar va el clip de soltar, por la ranura de acción
    public AnimationClip HoldImpactClip => null;

    public override bool AimsCameraWhileHeld => AimBeforeThrow;

    // El clip que se ve al soltar (y del que salen los tiempos del lanzamiento apuntado).
    public AnimationClip ReleaseClip => AimReleaseClip != null ? AimReleaseClip : AnimationClip;

    // Para el RPC de animación: el paso 1 es soltar después de apuntar.
    public override AnimationClip GetStepAnimationClip(int sequenceIndex, int stepIndex)
        => stepIndex == 1 ? ReleaseClip : AnimationClip;

    // Valida, reproduce la animación de disparo (con o sin PlayerController)
    // y arranca la secuencia de disparo.
    public override void Activate()
    {
        if (!IsServer) return;   // ← NUEVO
        if (!CanActivate()) return;

        if (AimBeforeThrow)
        {
            StartAiming();
            return;
        }

        // OJO: acá NO se cobra. El cooldown empieza cuando el hacha SALE de la mano, no
        // cuando arranca la animación — ver ShootSequence.

        if (OwnerASC != null)
        {
            PlayerController pc = OwnerASC.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.RotateToAim();   // mirar hacia donde se lanza, igual que los ataques melee
                pc.PlayAnimation(this);
            }
            else
            {
                // Sin PlayerController (un NPC): se le habla directo al Animator. Los
                // parámetros se chequean antes porque el Animator de un NPC no tiene por
                // qué tener el esquema del jugador — el del fantasma, por ejemplo, no
                // tiene "ActionID". Sin este guard, Unity escupe un warning por CADA
                // disparo y termina tapando la consola.
                Animator anim = OwnerASC.GetComponent<Animator>();
                if (anim == null) anim = OwnerASC.GetComponentInChildren<Animator>();
                if (anim != null)
                {
                    if (HasAnimatorParameter(anim, "ActionID", AnimatorControllerParameterType.Int))
                        anim.SetInteger("ActionID", AnimationID);

                    if (HasAnimatorParameter(anim, AnimationTriggerName, AnimatorControllerParameterType.Trigger))
                        anim.SetTrigger(AnimationTriggerName);
                }
            }

            OwnerASC.StartAbilityCoroutine(ShootSequence());

            // Arrancó de verdad, aunque cobre recién al soltar: sin esto el servidor no
            // les manda la animación a los demás (ver StartedThisActivation).
            StartedThisActivation = true;
        }
    }

    // =========================================================
    // APUNTAR (AimBeforeThrow)
    // =========================================================

    // Apretar: el arma atrás en bucle. Nada se cobra hasta soltar.
    private void StartAiming()
    {
        if (_aiming || OwnerASC == null) return;

        _aiming       = true;
        _aimStartedAt = Time.time;

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null) pc.PlayHoldAnimation(this);

        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc != null)
            netAsc.ServerBroadcastHoldAnimation(this, NetworkAbilitySystemComponent.EHoldAnimationPhase.Start);

        OwnerASC.StartAbilityCoroutine(AimWatch());
    }

    // Vigila el apuntado en el servidor: un aturdido o la muerte lo cortan SIN lanzar (y
    // sin cobrar), y el corte de seguridad lanza si el aviso de soltar nunca llega.
    private IEnumerator AimWatch()
    {
        while (_aiming)
        {
            if (OwnerASC == null || OwnerASC.HasTag(EGameplayTag.State_Dead) ||
                OwnerASC.HasTag(EGameplayTag.State_Stunned))
            {
                CancelAim();
                yield break;
            }

            if (AimSafetyTimeout > 0f && Time.time - _aimStartedAt >= AimSafetyTimeout)
            {
                EndHold();
                yield break;
            }
            yield return null;
        }
    }

    // Soltar (IHoldAbility.EndHold, lo pide el dueño al soltar el botón): lanza YA.
    // Idempotente.
    public void EndHold()
    {
        if (!_aiming || OwnerASC == null) return;

        if (OwnerASC.HasTag(EGameplayTag.State_Dead) || OwnerASC.HasTag(EGameplayTag.State_Stunned))
        {
            CancelAim();
            return;
        }

        _aiming = false;
        StopAimAnimation();

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null) pc.RotateToAim();

        // El lanzamiento desde la pose: al dueño ya se lo mostró su propia predicción
        // (PredictOwnerReleaseVisuals); a los demás les llega por acá.
        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc != null) netAsc.ServerBroadcastStepAnimationToOthers(this, 0, 1, this);
        else if (pc != null) pc.PlayActionClip(ReleaseClip, 1f, AnimationTriggerName, AnimationID);

        OwnerASC.StartAbilityCoroutine(ShootSequence(fromAim: true));
    }

    // Cortado sin lanzar: no sale nada ni se cobra nada.
    private void CancelAim()
    {
        if (!_aiming) return;
        _aiming = false;
        StopAimAnimation();
        EndAbility();
    }

    private void StopAimAnimation()
    {
        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null) pc.StopHoldAnimation();

        NetworkAbilitySystemComponent netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc != null)
            netAsc.ServerBroadcastHoldAnimation(this, NetworkAbilitySystemComponent.EHoldAnimationPhase.Stop);
    }

    // El dueño soltó el botón: muestra el lanzamiento en el acto, sin esperar al
    // servidor, y esconde el arma en el mismo momento en que el servidor la va a soltar.
    public override void PredictOwnerReleaseVisuals(PlayerController pc)
    {
        if (pc == null || !AimBeforeThrow) return;

        pc.PlayActionClip(ReleaseClip, 1f, AnimationTriggerName, AnimationID);

        if (!HideWeaponWhileFlying) return;
        ResolveThrowTiming(true, out float releaseDelay, out float backswing);
        pc.PredictWeaponHide(releaseDelay, releaseDelay + backswing);
    }

    // ¿Este Animator tiene ese parámetro, del tipo esperado? Pedirle algo que no tiene
    // no rompe nada, pero llena la consola de warnings.
    private static bool HasAnimatorParameter(Animator anim, string parameterName,
                                             AnimatorControllerParameterType type)
    {
        if (anim == null || string.IsNullOrEmpty(parameterName)) return false;

        foreach (AnimatorControllerParameter p in anim.parameters)
            if (p.type == type && p.name == parameterName) return true;

        return false;
    }

    // Le pide a la capa de red que muestre/oculte el arma del lanzador en TODOS los
    // peers. Sin NetworkASC (un NPC suelto, o pruebas sin red) se aplica local.
    private void SetOwnerWeaponVisible(bool visible)
    {
        if (OwnerASC == null) return;

        var netAsc = OwnerASC.GetComponent<NetworkAbilitySystemComponent>();
        if (netAsc != null) { netAsc.ServerSetWeaponVisible(visible); return; }

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();
        if (pc != null) pc.SetMainWeaponVisible(visible);
    }

    // Espera SpawnDelay (ajustado por velocidad de ataque), suelta el
    // proyectil, espera el remate de la animación, y termina.
    // SE PUEDE CORTAR A MITAD, si el asset trae IsInterruptible. Hay dos momentos y
    // significan cosas distintas:
    //
    //   · ANTES de soltar  → el proyectil no sale. El cooldown ya pagado no se devuelve:
    //     cancelaste, y cancelar cuesta. Es la finta.
    //   · DESPUÉS de soltar → el proyectil ya está en el aire y se queda. Lo único que
    //     se saltea es el remate de la animación, así se puede encadenar otra habilidad
    //     de inmediato en vez de esperar a que el brazo vuelva.
    //
    // En los dos casos se sale SIN EndAbility: el "fin" lo manda la habilidad nueva, que
    // es la que pasa a mandar (ver GameplayAbility.IsInterruptible).
    private IEnumerator ShootSequence(bool fromAim = false)
    {
        ResolveThrowTiming(fromAim, out float releaseDelay, out float backswing);

        int cancelSerial = OwnerASC != null ? OwnerASC.CancelSerial : 0;

        if (releaseDelay > 0f) yield return new WaitForSeconds(releaseDelay);

        if (Cancelled(cancelSerial))
        {
            // Nunca se escondió el arma (eso pasa al soltar), así que no hay nada que
            // devolver: solo no sale el proyectil. Y tampoco se cobra nada — la finta
            // sale gratis en cooldown, ver abajo por qué eso no se puede abusar.
            yield break;
        }

        // EL COOLDOWN EMPIEZA ACÁ, no al apretar el botón.
        //
        // Cortar el lanzamiento antes de tiempo te dejaba igual sin habilidad por varios
        // segundos: pagabas el precio de un hacha que nunca salió. Cobrando al soltar, lo
        // que se paga es el hacha, no la intención.
        //
        // POR QUÉ NO SE PUEDE ABUSAR: para cancelar hay que activar OTRA habilidad, y esa
        // tiene su propio costo. No existe un botón de "cancelar" suelto. Y repetir el
        // botón del lanzamiento no reinicia nada, porque una habilidad no se corta a sí
        // misma (ver PlayerController.CheckAbilityButton). Lo peor que se puede hacer es
        // amagar gastando el cooldown de otra cosa, que es un mal negocio.
        //
        // EFECTO EN EL BALANCE: entre dos hachas ahora pasa (tiempo de soltar + cooldown)
        // en vez de solo el cooldown. Son unos 0,7 s más en el hacha del Bárbaro. Si se
        // siente lenta, se baja su CooldownDuration.
        CommitAbility();

        SpawnProjectile();

        // El arma real desaparece justo al soltar: de ahi en mas lo unico que vuela es
        // el proyectil (ver HideWeaponWhileFlying).
        if (HideWeaponWhileFlying) SetOwnerWeaponVisible(false);

        // El remate se espera de a pedacitos para poder mirar la cancelación en el
        // medio. Con un solo WaitForSeconds habría que aguantar el remate entero aunque
        // el jugador ya hubiera apretado otra cosa, que es justo lo que queremos evitar.
        float waited = 0f;
        while (waited < backswing)
        {
            yield return null;
            waited += Time.deltaTime;

            if (!Cancelled(cancelSerial)) continue;

            // El arma vuelve YA a la mano: la rutina que la devolvía no va a terminar.
            if (HideWeaponWhileFlying) SetOwnerWeaponVisible(true);
            yield break;
        }

        if (HideWeaponWhileFlying) SetOwnerWeaponVisible(true);

        EndAbility();
    }

    private bool Cancelled(int cancelSerial)
        => IsInterruptible && OwnerASC != null && OwnerASC.CancelSerial != cancelSerial;

    // El dueño hace la MISMA cuenta de su lado y esconde el arma cuando su propia
    // animacion suelta, sin esperar el aviso del servidor — que le llega dos viajes
    // tarde y le dejaba el hacha en la mano mientras el proyectil ya volaba.
    public override void PredictOwnerVisuals(PlayerController pc)
    {
        if (pc == null || !HideWeaponWhileFlying) return;

        ResolveThrowTiming(false, out float releaseDelay, out float backswing);
        pc.PredictWeaponHide(releaseDelay, releaseDelay + backswing);
    }

    // Cuando sale el proyectil (releaseDelay) y cuanto dura el remate hasta devolver el
    // arma a la mano (backswing). Lo calculan por igual el servidor y el dueño: los dos
    // datos salen del asset y del ritmo de ataque, que estan de los dos lados.
    //
    // fromAim: el lanzamiento soltado después de apuntar. Los tiempos salen del clip de
    // soltar (que ya arranca con el arma atrás), a su velocidad natural.
    private void ResolveThrowTiming(bool fromAim, out float releaseDelay, out float backswing)
    {
        if (fromAim)
        {
            AnimationClip clip = ReleaseClip;
            releaseDelay = AimReleaseDelay;
            if (clip != null)
                foreach (AnimationEvent evt in clip.events)
                    if (evt.functionName == HitFrameEventName) { releaseDelay = evt.time; break; }

            backswing = clip != null ? Mathf.Max(0.1f, clip.length - releaseDelay) : 0.4f;
            return;
        }

        float speedMultiplier = 1f;
        float atkSpeedStat = OwnerASC != null ? OwnerASC.GetAttributeValue(EAttributeType.AtkSpeed) : 0f;
        if (atkSpeedStat > 0) speedMultiplier = 1f / atkSpeedStat;

        // EL MOMENTO DE SOLTAR SALE DEL PROPIO CLIP, no de un numero a mano.
        //
        // Es el mismo mecanismo que ya usan los ataques melee: el evento
        // AnimationEvent_HitFrame marca en la animacion el frame exacto en el que la
        // mano suelta, y el servidor lo lee del asset (no depende de que ningun
        // cliente reporte nada). Asi el proyectil nace EXACTAMENTE en el frame en que
        // el arma sale de la mano, y como el arma real se esconde en ese mismo frame
        // —sin ningun yield en el medio— el relevo es invisible: lo que se ve es una
        // sola hacha que pasa de la mano al aire.
        //
        // Ademas se escala con ResolveAnimationSpeed, asi que si el ritmo de ataque
        // comprime el clip el disparo se adelanta en la misma proporcion y NUNCA se
        // desfasa. Un SpawnDelay fijo se desincroniza en cuanto cambia la velocidad.
        //
        // Sin eventos en el clip se cae a SpawnDelay, el comportamiento de siempre.
        List<float> releaseTimes = GetHitFrameTimes();

        float animSpeed = ResolveAnimationSpeed();
        if (animSpeed <= 0f) animSpeed = 1f;

        // Sin eventos, SpawnDelay se escala con la velocidad impuesta si la hay (un paso
        // de combo o de un cargado con AnimationSpeedOverride).
        releaseDelay = releaseTimes.Count > 0
            ? releaseTimes[0] / animSpeed
            : SpawnDelay / (AnimationSpeedOverride > 0f ? AnimationSpeedOverride : speedMultiplier);

        float backswingTime = 0.5f;
        backswing = backswingTime / speedMultiplier;

        // Con el arma escondida hay que esperar a que el CLIP TERMINE antes de
        // devolverla. El hueso de prop se va con el lanzamiento y regresa solo a la
        // mano dentro de la propia animacion: si la prendemos a mitad de ese regreso,
        // se ve el arma "volviendo" por el aire hasta la mano — el gesto raro del
        // final. Al esperar al final del clip, reaparece recien cuando el hueso ya
        // esta de vuelta en su lugar y el cambio no se nota.
        //
        // La espera solo se extiende cuando el arma esta escondida; el resto de los
        // disparos (magos, etc.) conservan el remate de siempre.
        if (HideWeaponWhileFlying && AnimationClip != null)
            backswing = Mathf.Max(backswing, (AnimationClip.length / animSpeed) - releaseDelay);
    }

    // Instancia el proyectil, lo spawnea en red, lo inicializa con los
    // datos de esta habilidad, y le da su velocidad inicial hacia el
    // punto de mira del dueño.
    private void SpawnProjectile()
    {
        // Sin prefab no hay nada que disparar. Sin este guard, Instantiate(null) tira
        // una excepción que corta la corutina y deja al jugador trabado en "atacando"
        // hasta que salta el watchdog — un fallo de configuración difícil de leer.
        if (ProjectilePrefab == null)
        {
            Debug.LogWarning($"[{AbilityName}] no tiene ProjectilePrefab asignado: no se dispara nada.");
            return;
        }

        Vector3    spawnPos       = OwnerASC.transform.TransformPoint(SpawnOffset);
        Quaternion spawnRot       = Quaternion.identity;
        Vector3    launchDirection = Vector3.forward;

        PlayerController pc = OwnerASC.GetComponent<PlayerController>();

        if (pc != null)
        {
            // La dirección la resuelve el PlayerController: no es (mira - mano) a secas,
            // porque de cerca eso sale torcido (ver GetLaunchDirection).
            launchDirection = pc.GetLaunchDirection(spawnPos, MinConvergeDistance);
            spawnRot        = Quaternion.LookRotation(launchDirection);
        }
        else
        {
            launchDirection = OwnerASC.transform.forward;
            spawnRot        = OwnerASC.transform.rotation;
        }

        GameObject newProjectile = Instantiate(ProjectilePrefab, spawnPos, spawnRot);

        // Esto corre en el servidor. Un Instantiate() normal solo crea el
        // objeto en ESTE proceso — nunca llega a los clientes (por eso el
        // proyectil nunca se veía para el dueño remoto). Hay que spawnearlo
        // en red igual que cualquier otro NetworkObject.
        NetworkObject projectileNob = newProjectile.GetComponent<NetworkObject>();
        if (projectileNob != null)
            InstanceFinder.ServerManager.Spawn(projectileNob);
        else
            Debug.LogWarning("[GA_ProjectileShoot] El ProjectilePrefab no tiene NetworkObject — no se va a replicar a los clientes.");

        GC_Projectile projectileScript = newProjectile.GetComponent<GC_Projectile>();
        if (projectileScript != null)
        {
            // El swap visual (cubo -> arma real) lo resuelve GC_Projectile
            // internamente en CADA peer a partir de quién disparó (ver
            // _shooterNob ahí) — llamarlo acá de nuevo solo pasaría en el
            // servidor y duplicaría el arma clonada en esa copia.
            //
            // Le pasamos 'this' para que, al impactar (siempre en el
            // servidor), el proyectil pueda pedirle a NetworkASC que
            // reproduzca ImpactVFX en todos los peers vía PlayImpactVFX().
            // TargetEffects: si está vacío el proyectil sigue
            // ignorando a los aliados como siempre; si tiene algo, se lo aplica a los
            // que atraviese (la estela del Castigo divino, que cura al pasar).
            projectileScript.Initialize(InstantDamageEffect, DurationEffect, OwnerASC, UltimateChargeAmount,
                                        ImpactVFX, this, AdditionalEffects, TargetEffects, LifeTime);
        }

        Rigidbody rb = newProjectile.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = launchDirection * LaunchForce;
            if (AddSpin) rb.AddRelativeTorque(Vector3.right * 1000f);
        }
    }

    // Vista previa de la TRAYECTORIA en el Editor: sale del mismo SpawnOffset y con
    // la misma velocidad (LaunchForce) que usa el disparo real, así que la curva que
    // ves es la que va a volar. Si el Rigidbody del prefab usa gravedad, simula el
    // arco balístico paso a paso hasta tocar el suelo; si no, dibuja la línea recta.
    public override void DrawGizmos(Transform origin)
    {
        if (origin == null) return;

        Vector3 start = origin.TransformPoint(SpawnOffset);
        Vector3 velocity = origin.forward * LaunchForce;

        // La gravedad la decide el Rigidbody del prefab (por defecto, sí la usa).
        bool useGravity = true;
        if (ProjectilePrefab != null)
        {
            Rigidbody rb = ProjectilePrefab.GetComponent<Rigidbody>();
            if (rb != null) useGravity = rb.useGravity;
        }

        Gizmos.color = new Color(0.3f, 0.8f, 1f, 0.9f);
        Gizmos.DrawWireSphere(start, 0.15f);

        if (!useGravity)
        {
            Vector3 end = start + origin.forward * 30f;
            Gizmos.DrawLine(start, end);
            Gizmos.DrawWireSphere(end, 0.3f);
            return;
        }

        // Integración simple del tiro parabólico (mismo modelo que la física).
        Vector3 point = start;
        const float step = 0.05f;
        for (int i = 0; i < 300; i++)
        {
            Vector3 next = point + velocity * step;
            velocity += Physics.gravity * step;
            Gizmos.DrawLine(point, next);
            point = next;

            // Cortamos al llegar a la altura del piso del lanzador.
            if (point.y <= origin.position.y) break;
        }
        Gizmos.DrawWireSphere(point, 0.3f);
    }

    // Instancia ImpactVFX en el punto de impacto. Llamado por GC_Projectile
    // al impactar (siempre desde el servidor, vía
    // NetworkAbilitySystemComponent.ServerPlayAbilityVFX) — y replicado a
    // cada cliente con su propia copia local de esta misma habilidad.
    public override void PlayImpactVFX(Vector3 position)
    {
        if (ImpactVFX == null) return;
        GameObject vfx = Instantiate(ImpactVFX, position, Quaternion.identity);
        Destroy(vfx, 1.0f);
    }

    // =========================================================
    // PRIMER IMPACTO (FirstHitEffects)
    // =========================================================

    // Desde cuándo se le puede volver a aplicar cada efecto a cada enemigo. Estático y
    // por (lanzador, enemigo, efecto) y no un campo de la instancia: un combo crea
    // copias NUEVAS de sus pasos en cada activación (GA_ComboSequence), así que un
    // registro guardado acá se perdería en cada ataque.
    private static readonly Dictionary<(AbilitySystemComponent, AbilitySystemComponent, GameplayEffect), float>
        _firstHitReadyAt = new Dictionary<(AbilitySystemComponent, AbilitySystemComponent, GameplayEffect), float>();

    // Le aplica los FirstHitEffects al primer enemigo que tocó un proyectil de esta
    // habilidad, si ya pasó su tiempo mínimo con ese enemigo. La llama GC_Projectile,
    // en el servidor.
    public void ApplyFirstHitEffects(AbilitySystemComponent target)
    {
        if (FirstHitEffects == null || target == null || OwnerASC == null) return;

        foreach (GameplayEffect effect in FirstHitEffects)
        {
            if (effect == null) continue;

            var key = (OwnerASC, target, effect);
            if (FirstHitCooldownPerTarget > 0f &&
                _firstHitReadyAt.TryGetValue(key, out float readyAt) && Time.time < readyAt) continue;

            target.ApplyGameplayEffect(effect, OwnerASC);
            if (FirstHitCooldownPerTarget > 0f) _firstHitReadyAt[key] = Time.time + FirstHitCooldownPerTarget;
        }
    }
}
