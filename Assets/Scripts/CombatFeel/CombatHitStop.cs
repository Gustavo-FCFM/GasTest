using System.Collections.Generic;
using UnityEngine;

// ============================================================
// CombatHitStop
//
// El parón de impacto: al conectar un golpe cuerpo a cuerpo, la animación del que ataca se
// congela un instante y sigue. Es lo que hace que el arma choque contra algo en vez de pasar a
// través. Solo se congela SU animación: en red no se puede detener el tiempo para todos, y el
// combate no depende del Animator, así que el golpe siguiente sale a su hora.
//
// El servidor avisa a todas las pantallas (NetworkASC.ObserversImpactStop) y cada una congela al
// personaje por su cuenta, según sus ajustes. Qué habilidades lo piden: GameplayAbility.StopsOnImpact.
//
// VIVE EN EL PREFAB DE PARTIDA (Match_UI ▸ CombatFeel). Sus números, en el asset CombatFeel.
// ============================================================
public class CombatHitStop : MonoBehaviour
{
    private class Frozen
    {
        public Animator Animator;
        public float    Speed;     // la de antes, para devolvérsela
        public float    EndsAt;
    }

    private readonly List<Frozen> _frozen = new List<Frozen>();

    private void OnEnable()  => CombatFeel.ImpactStop += Freeze;

    private void OnDisable()
    {
        CombatFeel.ImpactStop -= Freeze;

        foreach (Frozen f in _frozen) Release(f);
        _frozen.Clear();
    }

    private void Freeze(Component attacker, float seconds)
    {
        if (!CombatFeel.HitStopOn || attacker == null || seconds <= 0f) return;

        PlayerController pc = attacker.GetComponent<PlayerController>();
        Animator animator = pc != null ? pc.characterAnimator : attacker.GetComponentInChildren<Animator>();
        if (animator == null) return;

        // Ya congelado: se alarga, sin pisar la velocidad que hay que devolverle.
        foreach (Frozen f in _frozen)
        {
            if (f.Animator != animator) continue;
            f.EndsAt = Mathf.Max(f.EndsAt, Time.time + seconds);
            return;
        }

        // Quieto por otra cosa (la Tumba helada): ese congelado no es nuestro.
        if (animator.speed <= 0f) return;

        _frozen.Add(new Frozen { Animator = animator, Speed = animator.speed, EndsAt = Time.time + seconds });
        animator.speed = 0f;
    }

    private void Update()
    {
        for (int i = _frozen.Count - 1; i >= 0; i--)
        {
            if (_frozen[i].Animator != null && Time.time < _frozen[i].EndsAt) continue;

            Release(_frozen[i]);
            _frozen.RemoveAt(i);
        }
    }

    private static void Release(Frozen f)
    {
        if (f.Animator != null) f.Animator.speed = f.Speed;
    }
}
