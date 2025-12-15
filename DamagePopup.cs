using UnityEngine;
using TMPro;

public class DamagePopup : MonoBehaviour
{
    public TMP_Text text;

    public float floatUpSpeed = 60f;
    public float lifeTime = 0.9f;

    public Color normalColor = Color.white;
    public Color critColor = new Color(1f, 0.2f, 0.2f, 1f);

    float t;
    Vector3 startPos;

    void Awake()
    {
        if (text == null) text = GetComponentInChildren<TMP_Text>();
        startPos = transform.localPosition;
    }

    public void Setup(int damage, bool crit)
    {
        if (text == null) return;

        if (crit)
        {
            text.color = critColor;
            text.text = $"{damage}\n<b>CRIT!</b>";
        }
        else
        {
            text.color = normalColor;
            text.text = damage.ToString();
        }

        t = 0f;
        startPos = transform.localPosition;
    }

    void Update()
    {
        t += Time.deltaTime;
        transform.localPosition = startPos + Vector3.up * floatUpSpeed * t;

        if (text != null)
        {
            float a = Mathf.Clamp01(1f - t / lifeTime);
            var c = text.color;
            c.a = a;
            text.color = c;
        }

        if (t >= lifeTime) Destroy(gameObject);
    }
}
