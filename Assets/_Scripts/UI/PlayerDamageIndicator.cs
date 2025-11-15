using _Scripts.Player.Runtime;
using UnityEngine;
using UnityEngine.UI;

public class PlayerDamageIndicator : MonoBehaviour
{
    [SerializeField] private RawImage damageIndicator;
    [SerializeField] private int maxAlpha = 150;
    
    private void Awake()
    {
        if (damageIndicator != null)
        {
            var color = damageIndicator.color;
            color.a = 0;
            damageIndicator.color = color;
        }
        PlayerHealth.OnTakeDamage += ChangeDamageIndicatorAlpha;
    }
    
    public void ChangeDamageIndicatorAlpha(float healthPercentage)
    {
        if (damageIndicator == null) return;

        int alphaValue = Mathf.Clamp((int)((1 - healthPercentage) * maxAlpha), 0, maxAlpha);
        var color = damageIndicator.color;
        color.a = alphaValue / 255f;
        damageIndicator.color = color;
    }
    
    private void OnDestroy()
    {
        PlayerHealth.OnTakeDamage -= ChangeDamageIndicatorAlpha;
    }
    
}
