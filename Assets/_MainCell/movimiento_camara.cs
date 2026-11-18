using UnityEngine;
using UnityEngine.InputSystem;

public class movimiento_camara : MonoBehaviour
{
    public float sensitivity = 2.0f;
    public float moveSpeed = 5.0f;
    private float rotationX = 0f;
    private float rotationY = 0f;

    void Start()
    {
        Vector3 rot = transform.localEulerAngles;
        rotationY = rot.y;
        rotationX = rot.x;
    }

    void Update()
    {
        Mouse currentMouse = Mouse.current;
        Keyboard currentKeyboard = Keyboard.current;

        if (currentMouse == null || currentKeyboard == null) return;

        // Rotar la vista con el clic derecho presionado
        if (currentMouse.rightButton.isPressed)
        {
            Vector2 mouseDelta = currentMouse.delta.ReadValue();
            rotationY += mouseDelta.x * sensitivity * 0.1f;
            rotationX -= mouseDelta.y * sensitivity * 0.1f;
            rotationX = Mathf.Clamp(rotationX, -90f, 90f);

            transform.localRotation = Quaternion.Euler(rotationX, rotationY, 0f);
        }

        // Movimiento libre con W, A, S, D
        float moveX = 0f;
        float moveZ = 0f;

        if (currentKeyboard.aKey.isPressed || currentKeyboard.leftArrowKey.isPressed) moveX -= 1f;
        if (currentKeyboard.dKey.isPressed || currentKeyboard.rightArrowKey.isPressed) moveX += 1f;
        if (currentKeyboard.wKey.isPressed || currentKeyboard.upArrowKey.isPressed) moveZ += 1f;
        if (currentKeyboard.sKey.isPressed || currentKeyboard.downArrowKey.isPressed) moveZ -= 1f;

        Vector3 moveDir = new Vector3(moveX, 0, moveZ).normalized;
        transform.Translate(moveDir * moveSpeed * Time.deltaTime, Space.Self);

        // --- RESTRICCIÓN DE LÍMITES (CLARK/CLAMP) ---
        // El piso mide 3m de ancho (X: -1.5 a 1.5) y 6m de largo (Z: -3 a 3).
        // Usamos un margen interno para que la cámara frene antes de tocar o atravesar las paredes.
        Vector3 posicionActual = transform.position;
        posicionActual.x = Mathf.Clamp(posicionActual.x, -1.3f, 1.3f);
        posicionActual.z = Mathf.Clamp(posicionActual.z, -2.8f, 2.8f);
        transform.position = posicionActual;
    }
}