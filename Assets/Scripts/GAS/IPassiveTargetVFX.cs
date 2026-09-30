// ============================================================
// IPassiveTargetVFX
//
// Una pasiva de clase (un componente del PassiveBehaviorsPrefab) que quiere mostrar un
// VFX sobre OTRO personaje, en todas las pantallas. El caso de hoy: la curación del
// aura del Paladín, que destella sobre cada aliado curado.
//
// La pasiva decide en el servidor y llama NetworkASC.ServerPlayPassiveVFXOn(objetivo).
// Por red viaja solo el objetivo: cada peer busca esta interfaz en SU copia del prefab
// de pasivas del jugador y ella dibuja el VFX con su propio prefab y ajustes. Así la
// capa de red no necesita saber qué pasiva es ni qué efecto tiene.
//
// Uno por jugador: si el prefab de pasivas trae varios, se usa el primero.
// ============================================================
public interface IPassiveTargetVFX
{
    void PlayPassiveVFX(AbilitySystemComponent target);
}
