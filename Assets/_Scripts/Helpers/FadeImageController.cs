using UnityEngine;
using UnityEngine.UI;

public class FadeImageController : MonoBehaviour
{
    public static FadeImageController Instance;
    public Image image;

    private void Start()
    {
        h.CreateStaticInstance(this, ref Instance);
    }
    
}
