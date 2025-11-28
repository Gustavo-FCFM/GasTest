using UnityEngine;

public class ClassChangerPod : MonoBehaviour
{
    public CharacterClassDefinition ClassToEquip; // Arrastra aquí "Class_Barbarian"

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            PlayerController pc = other.GetComponent<PlayerController>();
            if (pc != null)
            {
                pc.EquipCharacterClass(ClassToEquip);
            }
        }
    }
}