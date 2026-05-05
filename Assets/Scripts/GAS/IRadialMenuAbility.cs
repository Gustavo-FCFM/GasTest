using UnityEngine;

public interface IRadialMenuAbility
{
    float MaxRadialRange { get; } 
    
    // NUEVO: La lista de imágenes a mostrar en el círculo
    Sprite[] RadialIcons { get; } 
    
    void ActivateWithSelection(int selectedIndex, Vector3 targetPosition);
}