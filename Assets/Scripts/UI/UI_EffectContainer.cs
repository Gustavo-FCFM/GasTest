using UnityEngine;
using System.Collections.Generic;

public class UI_EffectContainer : MonoBehaviour
{
    [Header("Configuración")]
    public AbilitySystemComponent TargetASC; // ¿A quién estamos vigilando?
    public GameObject EffectSlotPrefab;      // El prefab con el script UI_EffectSlot

    // Diccionario para no destruir y crear objetos a lo loco cada frame
    private Dictionary<ActiveGameplayEffect, UI_EffectSlot> activeSlots = new Dictionary<ActiveGameplayEffect, UI_EffectSlot>();

    void Update()
    {
        if (TargetASC == null) return;

        UpdateContainer();
    }

    // Método público para asignar el ASC dinámicamente (útil para enemigos al spawnear)
    public void SetTargetASC(AbilitySystemComponent asc)
    {
        TargetASC = asc;
    }

    private void UpdateContainer()
    {
        // 1. Obtener lista de efectos actuales del ASC
        // NOTA: Necesitamos acceder a la lista 'ActiveEffects' de tu ASC.
        // Como es 'private' en tu código original, asumo que crearás un método GetActiveEffects() 
        // o cambiarás la lista a 'public' temporalmente. 
        // Aquí uso un getter hipotético: TargetASC.GetActiveEffects()
        
        List<ActiveGameplayEffect> currentEffects = TargetASC.GetActiveEffects();

        // Lista auxiliar para detectar cuáles borrar
        List<ActiveGameplayEffect> toRemove = new List<ActiveGameplayEffect>();

        // 2. Eliminar Slots de efectos que ya expiraron
        foreach (var pair in activeSlots)
        {
            if (!currentEffects.Contains(pair.Key))
            {
                toRemove.Add(pair.Key);
            }
        }

        foreach (var effect in toRemove)
        {
            Destroy(activeSlots[effect].gameObject);
            activeSlots.Remove(effect);
        }

        // 3. Crear Slots para efectos nuevos
        foreach (var effect in currentEffects)
        {
            // Si es tipo "Hidden", lo ignoramos
            if (effect.Definition.EffectType == GameplayEffect.EEffectType.Hidden) continue;

            // Si no tiene slot todavía, lo creamos
            if (!activeSlots.ContainsKey(effect))
            {
                GameObject newSlotObj = Instantiate(EffectSlotPrefab, transform);
                UI_EffectSlot newSlotScript = newSlotObj.GetComponent<UI_EffectSlot>();
                
                newSlotScript.Setup(effect);
                activeSlots.Add(effect, newSlotScript);
            }
        }
    }
}