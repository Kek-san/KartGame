using System;
using UnityEngine;
using Utilities;

[System.Serializable]
public class AxleInfo {
    public WheelCollider leftWheel;
    public WheelCollider rightWheel;
    public bool motor;
    public bool steering;
    public WheelFrictionCurve originalForwardFriction;
    public WheelFrictionCurve originalSidewaysFriction;
}

public class KartController : MonoBehaviour
{
    [Header("AxleInformation")]
    [SerializeField] AxleInfo[] _axleInfos;

    [Header("Motor Attributes")]
    [SerializeField] float _maxMotorTorque = 3000f;
    [SerializeField] float _maxSpeed;

    [Header("Steering Attributes")]
    [SerializeField] float _maxSteeringAngle = 30f;

    [Header("Braking and Drifting Attributes")]
    [SerializeField] float brakeTorque = 10000f;


    [SerializeField] InputReader _input;
    Rigidbody _rb;

    float brakeVelocity;

    private void Start() {
        _rb = GetComponent<Rigidbody>();
        _input.Enable();

        foreach(var axleInfo in _axleInfos) {
            axleInfo.originalForwardFriction = axleInfo.leftWheel.forwardFriction;
            axleInfo.originalSidewaysFriction = axleInfo.leftWheel.sidewaysFriction;
        }
    }



    private void FixedUpdate() {
        Vector2 vectorInput = _input.Move.normalized;

        float motor = _maxMotorTorque * vectorInput.y;
        float steering = _maxSteeringAngle * vectorInput.x;

        UpdateAxles(motor, steering);
    }

    private void UpdateAxles(float motor, float steering) {
       foreach(AxleInfo axleInfo in _axleInfos) {
            HandleSteering(steering, axleInfo);
            HandleMotor(motor, axleInfo);
            HandleBrakesAndDrift(axleInfo);
            UpdateWheelVisuals(axleInfo.leftWheel);
            UpdateWheelVisuals(axleInfo.rightWheel);
        }
    }
    private void HandleMotor(float motor, AxleInfo axleInfo) {
        if (axleInfo.motor) {
            axleInfo.leftWheel.motorTorque = motor;
            axleInfo.rightWheel.motorTorque = motor;
        }
    }
    private void HandleSteering(float steering, AxleInfo axleInfo) {
        if (axleInfo.steering) {
            axleInfo.leftWheel.steerAngle = steering;
            axleInfo.rightWheel.steerAngle = steering;
        }
    }
    private void HandleBrakesAndDrift(AxleInfo axleInfo) {
        if (axleInfo.motor) {
            if (_input.IsBraking) {
                _rb.constraints = RigidbodyConstraints.FreezeRotationX;

                float newZ = Mathf.SmoothDamp(_rb.linearVelocity.z, 0, ref brakeVelocity, 1f);
                _rb.linearVelocity = _rb.linearVelocity.With(z: newZ);

                axleInfo.leftWheel.brakeTorque = brakeTorque;
                axleInfo.rightWheel.brakeTorque = brakeTorque;
            } else {
                _rb.constraints = RigidbodyConstraints.None;

                axleInfo.leftWheel.brakeTorque = 0f;
                axleInfo.rightWheel.brakeTorque = 0f;
            }
        }
    }
    private void UpdateWheelVisuals(WheelCollider collider) {
        if (collider.transform.childCount == 0) return;

        Transform wheelVisual = collider.transform.GetChild(0);

        Vector3 position;
        Quaternion rotation;
        collider.GetWorldPose(out position, out rotation);

        wheelVisual.position = position;
        wheelVisual.rotation = rotation;
    }

}
