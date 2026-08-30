using System.Globalization;
using TMPro;
using UnityEngine;

public class WorldDamagePopup : MonoBehaviour
{
    [SerializeField] private TMP_Text text;
    [SerializeField] private float riseSpeed = 1.5f;
    [SerializeField, Min(0.05f)] private float lifeTime = 0.8f;

    private float timer;
    private Color startColor;
    private Camera mainCamera;
    private PooledObject pooledObject;

    private void Awake()
    {
        if (text == null)
            text = GetComponent<TMP_Text>();

        mainCamera = Camera.main;
        pooledObject = GetComponent<PooledObject>();
    }

    private void OnEnable()
    {
        timer = 0f;
    }

    public void Init(float amount, WeaponDamageType damageType)
    {
        text.text = Mathf.RoundToInt(amount).ToString(CultureInfo.InvariantCulture);
        startColor = damageType.GetDisplayColor();
        startColor.a = 1f;
        text.color = startColor;
    }

    private void Update()
    {
        if (mainCamera != null)
        {
            transform.LookAt(
                transform.position + mainCamera.transform.rotation * Vector3.forward,
                mainCamera.transform.rotation * Vector3.up);
        }

        transform.position += Vector3.up * riseSpeed * Time.deltaTime;

        timer += Time.deltaTime;
        float progress = Mathf.Clamp01(timer / lifeTime);

        Color currentColor = startColor;
        currentColor.a = 1f - progress;
        text.color = currentColor;

        if (timer >= lifeTime)
            pooledObject.Release();
    }
}
