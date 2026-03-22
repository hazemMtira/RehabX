using UnityEngine;
using TMPro;
using System.Collections;

public class FloatingText : MonoBehaviour
{
    public TextMeshPro textMesh;
    public float floatSpeed = 1f;
    public float fadeSpeed = 1f;
    public float lifetime = 1f;

    private Color startColor;

    public void Initialize(string text, Color color)
    {
        textMesh.text = text;
        textMesh.color = color;
        startColor = color;
        StartCoroutine(FloatAndFade());
    }

    IEnumerator FloatAndFade()
    {
        float elapsed = 0f;
        Vector3 startPos = transform.position;

        while (elapsed < lifetime)
        {
            elapsed += Time.deltaTime;
            float progress = elapsed / lifetime;

            // Float upward
            transform.position = startPos + Vector3.up * (floatSpeed * elapsed);

            // Fade out
            Color newColor = startColor;
            newColor.a = 1f - progress;
            textMesh.color = newColor;

            yield return null;
        }

        Destroy(gameObject);
    }
}