using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using XRInputDevice = UnityEngine.XR.InputDevice;
using XRCommonUsages = UnityEngine.XR.CommonUsages;

namespace DataCenterRescue
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class DCRPlayerController : MonoBehaviour
    {
        private DCRGameManager manager;
        private Camera playerCamera;
        private CharacterController characterController;
        private float verticalSpeed;
        private float pitch;
        private float nextSnapTurn;
        private XRInputDevice headDevice;
        private XRInputDevice leftHand;
        private XRInputDevice rightHand;

        public bool IsXRActive => XRSettings.isDeviceActive;
        public XRInputDevice RightHand => rightHand;
        public XRInputDevice LeftHand => leftHand;

        public void Initialize(DCRGameManager gameManager, Camera cameraReference, CharacterController controller)
        {
            manager = gameManager;
            playerCamera = cameraReference;
            characterController = controller;
            pitch = 0f;
            RefreshDevices();
        }

        private void Update()
        {
            if (manager == null || playerCamera == null || characterController == null)
                return;

            if (IsXRActive)
                UpdateXRTracking();

            if (!manager.CanMovePlayer)
                return;

            if (IsXRActive)
                UpdateXRMovement();
            else
                UpdateDesktopMovement();
        }

        public void ResetDesktopView()
        {
            pitch = 0f;
            if (playerCamera != null)
                playerCamera.transform.localRotation = Quaternion.identity;
        }

        private void RefreshDevices()
        {
            headDevice = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        }

        private void UpdateXRTracking()
        {
            if (!headDevice.isValid || !leftHand.isValid || !rightHand.isValid)
                RefreshDevices();

            if (headDevice.isValid)
            {
                if (headDevice.TryGetFeatureValue(XRCommonUsages.devicePosition, out Vector3 headPosition))
                    playerCamera.transform.localPosition = headPosition;
                if (headDevice.TryGetFeatureValue(XRCommonUsages.deviceRotation, out Quaternion headRotation))
                    playerCamera.transform.localRotation = headRotation;
            }
        }

        private void UpdateDesktopMovement()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            if (keyboard == null) return;

            if (manager.CursorIsLocked && mouse != null)
            {
                Vector2 delta = mouse.delta.ReadValue();
                float sensitivity = manager.LookSensitivity;
                transform.Rotate(0f, delta.x * sensitivity, 0f, Space.World);

                float vertical = delta.y * sensitivity * (manager.InvertY ? 1f : -1f);
                pitch = Mathf.Clamp(pitch + vertical, -78f, 78f);
                playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }

            Vector2 moveInput = Vector2.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) moveInput.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) moveInput.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) moveInput.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) moveInput.x -= 1f;

            float speed = manager.MoveSpeed;
            if (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed)
                speed *= 1.55f;

            MoveCharacter(moveInput, speed, false);
        }

        private void UpdateXRMovement()
        {
            Vector2 moveInput = Vector2.zero;
            if (leftHand.isValid)
                leftHand.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out moveInput);

            float speed = manager.MoveSpeed;
            bool sprint = false;
            if (leftHand.isValid)
                leftHand.TryGetFeatureValue(XRCommonUsages.gripButton, out sprint);
            if (sprint) speed *= 1.4f;
            MoveCharacter(moveInput, speed, true);

            if (rightHand.isValid && Time.time >= nextSnapTurn)
            {
                if (rightHand.TryGetFeatureValue(XRCommonUsages.primary2DAxis, out Vector2 turnAxis))
                {
                    if (Mathf.Abs(turnAxis.x) > 0.75f)
                    {
                        transform.Rotate(Vector3.up, Mathf.Sign(turnAxis.x) * manager.SnapTurnDegrees, Space.World);
                        nextSnapTurn = Time.time + 0.32f;
                    }
                }
            }
        }

        private void MoveCharacter(Vector2 input, float speed, bool headDirected)
        {
            input = Vector2.ClampMagnitude(input, 1f);
            Transform directionReference = headDirected ? playerCamera.transform : transform;
            Vector3 direction = directionReference.forward * input.y + directionReference.right * input.x;
            direction.y = 0f;

            if (characterController.isGrounded && verticalSpeed < 0f)
                verticalSpeed = -1.5f;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;

            Vector3 movement = direction * speed + Vector3.up * verticalSpeed;
            characterController.Move(movement * Time.deltaTime);
        }
    }
}
