using UnityEngine;

// ============================================================
// ClassChangerPod
//
// Trigger de la arena que, al pisarlo un jugador, le cambia la
// clase equipada (ClassToEquip). No es un NetworkBehaviour (existe
// igual en la física local de todos los peers), así que reacciona
// solo al dueño real del jugador para no desincronizar el cambio de
// clase del resto de la partida.
// ============================================================
public class ClassChangerPod : MonoBehaviour
{
    // Clase que se equipa al pisar este pod.
    public CharacterClassDefinition ClassToEquip;

    // Al entrar un jugador (y solo si somos su dueño), le equipa
    // ClassToEquip.
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController pc = other.GetComponent<PlayerController>();

            // Este trigger no es un NetworkBehaviour, así que existe (y puede
            // dispararse) en la física local de todos los peers, incluido el
            // servidor viendo a un jugador remoto pisarlo. EquipCharacterClass()
            // solo notifica al servidor (ServerSetClass) cuando IsOwner es true;
            // si dejamos que un peer no-dueño lo dispare, cambia armas/habilidades
            // solo en esa copia local sin avisar al servidor, y queda desincronizado
            // del resto de la partida.
            if (pc != null && pc.IsOwner)
            {
                pc.EquipCharacterClass(ClassToEquip);
            }
        }
    }
}
