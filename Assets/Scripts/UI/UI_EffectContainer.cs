using UnityEngine;
using System.Collections.Generic;

// ============================================================
// UI_EffectContainer
//
// Crea y destruye los UI_EffectSlot de la barra de buffs/debuffs de
// un personaje, siguiendo qué efectos tiene activos en cada momento.
// Igual que los slots de habilidad: si el objetivo tiene
// NetworkAbilitySystemComponent, lee los índices/duración
// sincronizados (funciona igual en host y en cliente remoto); si no
// (ej. un Dummy de entrenamiento sin red), lee el ASC local directo.
// ============================================================
public class UI_EffectContainer : MonoBehaviour
{
    [Header("Configuración")]
    public AbilitySystemComponent TargetASC; // A quién se le muestran los efectos
    public GameObject EffectSlotPrefab;      // Prefab con el script UI_EffectSlot

    private NetworkAbilitySystemComponent netAsc;
    private bool netAscChecked = false;

    // Modo local: un slot por instancia de ActiveGameplayEffect (vive en este proceso).
    private Dictionary<ActiveGameplayEffect, UI_EffectSlot> localSlots = new Dictionary<ActiveGameplayEffect, UI_EffectSlot>();

    // Modo en red: un slot por índice en GameplayEffectRegistry.
    private Dictionary<int, UI_EffectSlot> netSlots = new Dictionary<int, UI_EffectSlot>();

    // Detecta una sola vez si el objetivo tiene red, y actualiza los
    // slots con el método correspondiente cada frame.
    void Update()
    {
        if (TargetASC == null) return;

        if (!netAscChecked)
        {
            netAsc = TargetASC.GetComponent<NetworkAbilitySystemComponent>();
            netAscChecked = true;
        }

        if (netAsc != null) UpdateNetworked();
        else UpdateLocal();
    }

    // Cambia a quién se le muestran los efectos en runtime (ej: un NPC
    // recién spawneado). Fuerza a re-detectar si tiene red o no.
    public void SetTargetASC(AbilitySystemComponent asc)
    {
        TargetASC = asc;
        netAscChecked = false;
    }

    // Crea/destruye slots comparando la lista real de ActiveEffects del
    // ASC contra los slots que ya existen. Usado cuando no hay red o
    // cuando este proceso ES el servidor (ActiveEffects está poblado de
    // verdad).
    private void UpdateLocal()
    {
        List<ActiveGameplayEffect> currentEffects = TargetASC.GetActiveEffects();

        List<ActiveGameplayEffect> toRemove = new List<ActiveGameplayEffect>();
        foreach (var pair in localSlots)
            if (!currentEffects.Contains(pair.Key)) toRemove.Add(pair.Key);

        foreach (var effect in toRemove)
        {
            Destroy(localSlots[effect].gameObject);
            localSlots.Remove(effect);
        }

        foreach (var effect in currentEffects)
        {
            if (effect.Definition.EffectType == GameplayEffect.EEffectType.Hidden) continue;
            if (localSlots.ContainsKey(effect)) continue;

            UI_EffectSlot slot = CreateSlot();
            slot.SetupLocal(effect);
            localSlots.Add(effect, slot);
        }
    }

    // Igual que UpdateLocal pero leyendo los índices sincronizados de
    // NetworkAbilitySystemComponent en vez de ActiveEffects — es el
    // camino que usa cualquier cliente remoto (y también el servidor,
    // que se escribe a sí mismo el mismo SyncDictionary).
    private void UpdateNetworked()
    {
        // Copiamos las claves porque GetActiveEffectIndices() expone el
        // SyncDictionary en vivo — no queremos iterarlo mientras cambia
        // por un paquete que llega a mitad de este método.
        HashSet<int> currentIndices = new HashSet<int>(netAsc.GetActiveEffectIndices());

        List<int> toRemove = new List<int>();
        foreach (var pair in netSlots)
            if (!currentIndices.Contains(pair.Key)) toRemove.Add(pair.Key);

        foreach (var index in toRemove)
        {
            Destroy(netSlots[index].gameObject);
            netSlots.Remove(index);
        }

        GameplayEffectRegistry registry = GameplayEffectRegistry.Instance;
        if (registry == null) return;

        foreach (int index in currentIndices)
        {
            GameplayEffect definition = registry.GetEffect(index);
            if (definition == null) continue;

            if (!netSlots.TryGetValue(index, out UI_EffectSlot slot))
            {
                slot = CreateSlot();
                slot.Setup(definition);
                netSlots.Add(index, slot);
            }

            if (netAsc.TryGetNetActiveEffect(index, out float remaining, out float total))
                slot.SetProgress(remaining, total);
        }
    }

    // Instancia un UI_EffectSlot nuevo como hijo de este contenedor.
    private UI_EffectSlot CreateSlot()
    {
        GameObject newSlotObj = Instantiate(EffectSlotPrefab, transform);
        return newSlotObj.GetComponent<UI_EffectSlot>();
    }
}
