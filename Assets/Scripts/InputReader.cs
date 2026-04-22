using UnityEngine;
using UnityEngine.InputSystem;


[CreateAssetMenu(fileName = "InputReader", menuName = "Kart/InputReader" )]
public class InputReader : ScriptableObject, KartInputSystem.IPlayerActions
{
    public Vector3 Move => _kartInputActions.Player.Move.ReadValue<Vector2>();
    public bool IsBraking => _kartInputActions.Player.Brake.ReadValue<float>() > 0;

    KartInputSystem _kartInputActions;

    private void OnEnable() {
       if(_kartInputActions == null) {
            _kartInputActions = new KartInputSystem();
            _kartInputActions.Player.SetCallbacks(this);
        }
       _kartInputActions.Enable();
    }

    private void OnDisable() {
        _kartInputActions.Disable();
    }

    public void Enable() {
        _kartInputActions.Enable();
    }


    public void OnBrake(InputAction.CallbackContext context) {
        //noop
    }

    public void OnFire(InputAction.CallbackContext context) {
        //noop
    }

    public void OnLook(InputAction.CallbackContext context) {
        //noop
    }

    public void OnMove(InputAction.CallbackContext context) {
        //noop
    }
}
