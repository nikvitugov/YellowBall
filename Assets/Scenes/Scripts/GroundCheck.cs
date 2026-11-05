using UnityEngine;

public class GroundCheck : MonoBehaviour
{
    public bool isGrounded = false;

    private int groundLayer;
    private int contacts; // считаем касания: между двумя платформами флаг не должен сбрасываться

    private void Awake()
    {
        groundLayer = LayerMask.NameToLayer("Collided");
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.layer != groundLayer) return;
        contacts++;
        isGrounded = true;
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.layer != groundLayer) return;
        contacts = Mathf.Max(0, contacts - 1);
        isGrounded = contacts > 0;
    }

    private void OnDisable()
    {
        contacts = 0;
        isGrounded = false;
    }
}
