using UnityEngine;
public class PlayerCamera : MonoBehaviour
{
    [SerializeField] private Camera mainCamera;
    [Space]
    [Header("Camera Controls")]
    [SerializeField] private float sensitivity = 0.75f;
    [SerializeField] [Range(0f, 90f)] private float cameraBounds = 80f;
    [SerializeField] private float cameraSmooth = 10f;

    private Vector3 _eulerAngles;

    public void Initialize(Transform target)
    {
        transform.position = target.position;
        transform.eulerAngles = _eulerAngles = target.eulerAngles;
    }

    public void UpdateRotation(Vector2 input)
    {
        _eulerAngles += new Vector3(-input.y, input.x) * sensitivity;
        _eulerAngles.x = Mathf.Clamp(_eulerAngles.x, -cameraBounds, cameraBounds);
        transform.eulerAngles = _eulerAngles;
    }

    public void UpdatePosition(Transform target)
    {
        transform.position = Vector3.Lerp
        (
            transform.position,
            target.position,
            1f - Mathf.Exp(-cameraSmooth * Time.deltaTime)
        );
    }
}