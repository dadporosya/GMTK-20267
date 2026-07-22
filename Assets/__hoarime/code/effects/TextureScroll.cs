using UnityEngine;

public class TextureScroll : MonoBehaviour
{
    [SerializeField] float scrollSpeedX;
    [SerializeField] float scrollSpeedY;
    private string textureName = "_MainTex"; 

    private Renderer objectRenderer;
    private Vector2 currentOffset;

    void Start()
    {
        objectRenderer = GetComponent<Renderer>();
        currentOffset = objectRenderer.material.GetTextureOffset(textureName);
    }

    void Update()
    {
        currentOffset.x += Time.deltaTime * scrollSpeedX; 
        currentOffset.y += Time.deltaTime * scrollSpeedY; 

        objectRenderer.material.SetTextureOffset(textureName, currentOffset);
    }
}
