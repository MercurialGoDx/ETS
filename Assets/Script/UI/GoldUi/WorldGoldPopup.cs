using TMPro;
using UnityEngine;

public class WorldGoldPopup : MonoBehaviour
{
    public TMP_Text text;
    public float riseSpeed = 1.5f;
    public float lifeTime = 0.8f;

    private float timer;
    private Color startColor;

    private Camera cam;

    private void Awake()
    {
        if (text == null)
            text = GetComponent<TMP_Text>();

        cam = Camera.main;
        startColor = text.color;
    }

    public void Init(int amount)
    {
        text.text = $"+{amount}";
        startColor = text.color;
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
            Destroy(gameObject);
    }
}
