using UnityEngine;
using UnityEngine.InputSystem;
public class InputTesting : MonoBehaviour
{
    public InputActionProperty testActionValue;
    public InputActionProperty testActionButton;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        float value = testActionValue.action.ReadValue<float>();
        Debug.Log("Input Action Value: " + value);
        bool buttonPressed = testActionButton.action.IsPressed();
        Debug.Log("Input Action Button Pressed: " + buttonPressed);
    }
}
