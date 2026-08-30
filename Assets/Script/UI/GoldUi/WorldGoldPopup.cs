using TMPro;
using UnityEngine;

public class WorldGoldPopup : MonoBehaviour
{
    public TMP_Text text;
    public float riseSpeed = 1.5f;
    public float lifeTime = 0.8f;

    private float timer;
    private Color startColor;
    private Color defaultColor;

    private Camera cam;

    private PooledObject pooledObject;

    private void Awake()
    {
        if (text == null)
            text = GetComponent<TMP_Text>();

        cam = Camera.main;
        defaultColor = text.color;
        startColor = defaultColor;

        pooledObject = GetComponent<PooledObject>();
    }

    private void OnEnable()
    {
        timer = 0f;
        // Восстанавливаем альфу текста
        Color c = startColor;
        c.a = 1f;
        text.color = c;
    }

    public void Init(int amount)
    {
        InitText($"+{amount}", defaultColor);
    }

    private void InitText(string value, Color color)
    {
        text.text = value;
        startColor = color;
        startColor.a = 1f;
        text.color = startColor;
    }

    private void Update()
    {
        if (cam != null)
        {
            // Поворачиваем текст к камере
            transform.LookAt(
                transform.position + cam.transform.rotation * Vector3.forward,
                cam.transform.rotation * Vector3.up
            );
        }
        transform.position += Vector3.up * riseSpeed * Time.deltaTime;

        timer += Time.deltaTime;
        float k = Mathf.Clamp01(timer / lifeTime);

        Color c = startColor;
        c.a = 1f - k;
        text.color = c;

        if (timer >= lifeTime)
            pooledObject.Release();
    }
}
