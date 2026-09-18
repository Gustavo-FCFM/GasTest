using System;
using UnityEngine;

// ============================================================
// SfxCue
//
// Un "sonido" del juego: uno o varios clips entre los que se elige al azar, con su
// volumen y una pizca de variación de tono. Es lo que se asigna en el Inspector en vez
// de un AudioClip pelado, por dos razones:
//
//  1) VARIACIÓN. Un paso, un golpe o un grito que suena siempre IGUAL se nota a los
//     diez segundos y suena a metrónomo. Con tres clips parecidos y ±8 % de tono, el
//     oído deja de notar la repetición.
//  2) VACÍO = SILENCIO. Un cue sin clips no hace nada y no se queja. Así el código
//     puede estar completo antes de que exista un solo archivo de audio, y cada sonido
//     se agrega arrastrando clips, sin tocar código.
//
// Se reproduce con AudioManager.Play(cue, posición) o AudioManager.PlayUI(cue).
// ============================================================
[Serializable]
public class SfxCue
{
    [Tooltip("Uno o varios clips. Con varios se elige uno al azar (sin repetir el último).")]
    public AudioClip[] Clips;

    [Range(0f, 1f)]
    public float Volume = 1f;

    [Tooltip("Variación de tono al azar, ± este valor. 0.08 = ±8 %, suficiente para que dos " +
             "golpes seguidos no suenen idénticos.")]
    [Range(0f, 0.5f)]
    public float PitchVariance = 0.08f;

    [Tooltip("Hasta qué distancia se escucha (solo para sonidos 3D). Un paso 20 m; un grito o " +
             "una explosión 40–60 m.")]
    public float MaxDistance = 35f;

    [NonSerialized] private int _lastIndex = -1;

    public bool IsEmpty
    {
        get
        {
            if (Clips == null) return true;
            foreach (AudioClip c in Clips) if (c != null) return false;
            return true;
        }
    }

    // Elige un clip al azar, evitando repetir el último cuando hay más de uno.
    public AudioClip Pick()
    {
        if (Clips == null || Clips.Length == 0) return null;
        if (Clips.Length == 1) return Clips[0];

        int index;
        int tries = 0;
        do
        {
            index = UnityEngine.Random.Range(0, Clips.Length);
        } while ((index == _lastIndex || Clips[index] == null) && ++tries < 8);

        _lastIndex = index;
        return Clips[index];
    }

    public float RandomPitch()
    {
        return PitchVariance <= 0f ? 1f : 1f + UnityEngine.Random.Range(-PitchVariance, PitchVariance);
    }
}
