using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Selectable))]
public class ButtonSFX : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
{
    [Header("Audio Source (куда проигрываем)")]
    [SerializeField] private AudioSource audioSource;

    [Header("Clips")]
    [SerializeField] private AudioClip hoverClip;
    [SerializeField] private AudioClip clickClip;

    [Header("Volume")]
    [Range(0f, 1f)]
    [SerializeField] private float hoverVolume = 1f;
    [Range(0f, 1f)]
    [SerializeField] private float clickVolume = 1f;

    [Header("Behavior")]
    [Tooltip("Если кнопка не interactable — не играть звук.")]
    [SerializeField] private bool ignoreWhenNotInteractable = true;

    private Selectable selectable;

    private void Awake()
    {
        selectable = GetComponent<Selectable>();

        // Удобный дефолт: если не назначили — попробуем найти в родителях
        if (audioSource == null)
            audioSource = GetComponentInParent<AudioSource>();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!CanPlay()) return;
        PlayOneShotSafe(hoverClip, hoverVolume);
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!CanPlay()) return;
        PlayOneShotSafe(clickClip, clickVolume);
    }

    private bool CanPlay()
    {
        if (audioSource == null) return false;

        if (ignoreWhenNotInteractable && selectable != null && !selectable.interactable)
            return false;

        return true;
    }

    private void PlayOneShotSafe(AudioClip clip, float volume)
    {
        if (clip == null) return;
        audioSource.PlayOneShot(clip, Mathf.Clamp01(volume));
    }
}
