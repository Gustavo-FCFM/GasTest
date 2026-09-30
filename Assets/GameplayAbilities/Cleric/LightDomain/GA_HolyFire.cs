using UnityEngine;
using System.Collections;
using System.Collections.Generic;

// ============================================================
// GA_HolyFire  (Quemadura santa — definitiva del Clérigo, Dominio de la luz)
//
// "Oleada de fuego en cono amplio que quema a los enemigos y sana a los aliados. Los
// aliados que ataquen a un enemigo afectado por ese fuego se curan."
//
// El cono es un GA_ConeAttack normal: DamageEffect es la quemadura de la Luz (daño con
// el tiempo) y TargetEffects su curación con el tiempo, así que eso sale configurando.
// Lo único que agrega este script es la MARCA: cada enemigo alcanzado queda marcado
// MarkDuration segundos (lo que dura la quemadura), y mientras tanto cada ALIADO del
// Clérigo que lo golpea se cura (AllyRewardEffects).
//
// Es el Enemigo jurado del Paladín (GA_SwornEnemy) para VARIOS a la vez: aquel marca a
// uno solo; un cono alcanza a muchos. Mismo gancho (OnTookDamage del marcado, en el
// servidor) y el mismo freno por aliado para que pegar rápido no cure de más.
// ============================================================
[CreateAssetMenu(fileName = "GA_HolyFire", menuName = "GAS/Cleric/Holy Fire")]
public class GA_HolyFire : GA_ConeAttack
{
    [Header("La Marca (aliados que lo golpean se curan)")]
    [Tooltip("Segundos que queda marcado cada enemigo alcanzado. Lo natural es lo mismo que " +
             "dura la quemadura.")]
    public float MarkDuration = 5f;

    [Tooltip("Efectos que recibe CADA aliado (o el Clérigo) que golpee a un marcado. " +
             "Normalmente una curación instantánea.")]
    public List<GameplayEffect> AllyRewardEffects;

    [Tooltip("Tiempo mínimo entre dos curaciones al MISMO aliado. Sin esto, una clase de " +
             "ataques rápidos se curaría mucho más que una de golpes lentos.")]
    public float RewardCooldownPerAlly = 0.2f;

    // Marcados ahora mismo y hasta cuándo; y la última curación de cada aliado.
    [System.NonSerialized] private Dictionary<AbilitySystemComponent, float> _markedUntil;
    [System.NonSerialized] private Dictionary<AbilitySystemComponent, float> _lastReward;

    protected override void OnEnemyHit(AbilitySystemComponent enemy)
    {
        if (enemy == null || OwnerASC == null || MarkDuration <= 0f) return;

        _markedUntil ??= new Dictionary<AbilitySystemComponent, float>();
        _lastReward  ??= new Dictionary<AbilitySystemComponent, float>();

        bool alreadyMarked = _markedUntil.ContainsKey(enemy);
        _markedUntil[enemy] = Time.time + MarkDuration;   // volver a alcanzarlo renueva la marca

        if (alreadyMarked) return;

        enemy.OnTookDamage += HandleMarkedTookDamage;
        OwnerASC.StartAbilityCoroutine(WatchMark(enemy));
    }

    // Suelta la marca al vencer, o antes si el marcado muere.
    private IEnumerator WatchMark(AbilitySystemComponent enemy)
    {
        while (enemy != null && _markedUntil != null &&
               _markedUntil.TryGetValue(enemy, out float until) && Time.time < until &&
               !enemy.HasTag(EGameplayTag.State_Dead))
            yield return null;

        if (enemy != null) enemy.OnTookDamage -= HandleMarkedTookDamage;
        if ((object)enemy != null) _markedUntil?.Remove(enemy);
    }

    // Alguien golpeó a un marcado. Si es aliado del Clérigo (o él mismo), se cura.
    private void HandleMarkedTookDamage(AbilitySystemComponent attacker)
    {
        if (attacker == null || OwnerASC == null) return;
        if (!OwnerASC.IsAllyOf(attacker, includeSelf: true)) return;

        if (_lastReward != null)
        {
            if (_lastReward.TryGetValue(attacker, out float last) &&
                Time.time - last < RewardCooldownPerAlly) return;
            _lastReward[attacker] = Time.time;
        }

        ApplyEffectsTo(AllyRewardEffects, attacker);
    }
}
