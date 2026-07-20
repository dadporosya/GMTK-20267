using UnityEngine;

public class TextAlertManager : MonoBehaviour
{
    public static TextAlertManager Instance;

    [Header("Damage Alert Settings")]
    [SerializeField] private AlertDamageText damageTextPfb;
    [SerializeField] private float holdDuration    = 0.6f;
    [SerializeField] private float fadeOutDuration = 0.4f;
    [SerializeField] private float speed           = 1f;
    [SerializeField] private float spread = 1f;
    
    private void Awake()
    {
        h.CreateStaticInstance(this, ref Instance);
    }

    public void CreateDamageAlert(int damage, Transform target, Color? color = null, string label = null)
    {
        AlertDamageText alert = Instantiate(
            damageTextPfb,
            target.position + h.RandomPositionInCircle(spread),
            Quaternion.identity);
        alert.damageText.text  = label != null ? $"{label}{damage}" : damage.ToString();
        alert.holdDuration    = holdDuration;
        alert.fadeOutDuration = fadeOutDuration;
        alert.speed           = speed;
        if (color.HasValue) alert.damageText.color = color.Value;
    }
}
