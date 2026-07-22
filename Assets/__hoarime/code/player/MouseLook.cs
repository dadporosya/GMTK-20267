using System.Collections;
using Unity.VisualScripting;
using UnityEngine;

public class MouseLook : MonoBehaviour
{
    [Header("mouse")]

    [SerializeField] float sensitivity;
    [SerializeField] float smoothing;

    [SerializeField] float rotationTime;
    [SerializeField] float cameraMoveTime;

    [SerializeField] float minY;
    [SerializeField] float maxY;
    [SerializeField] float yOffset;

    [SerializeField] float minX;
    [SerializeField] float maxX;
    [SerializeField] bool limitX;

    public bool canLook = true;
    private bool frozen;

    [Header("camera bob")]

    [SerializeField] bool cameraBob;

    [SerializeField] float bobFrequency;
    [SerializeField] float bobAmount;
    [SerializeField] float bobSmoothing;

    [SerializeField] float magnitudeThreshold;
    [SerializeField] float walkBobMultiplier;
    [SerializeField] float walkReturnSpeed;

    private float bobMultiplier;

    private Vector3 startingBobPos;


    private Monologue monologue;
    private Movement movement;
    private CharacterController characterController;

    private GameObject camHolder;

    private float yRot;
    private float xRot;

    private Camera cam;

    private bool isAnimating;

    public bool Animating
    {
        get
        {
            return isAnimating;
        }
    }




    private void Awake()
    {
        Cursor.lockState = CursorLockMode.Locked;
        cam = Camera.main;
        camHolder = cam.transform.parent.gameObject;
        monologue = GetComponent<Monologue>();
        movement = GetComponent<Movement>();
        characterController = GetComponent<CharacterController>();
        startingBobPos = camHolder.transform.localPosition;
        ResetMouseLookRotation();
        bobMultiplier = 1;
    }

    private void Update()
    {
        if (!frozen)
        {
            XMouseLook();
            YMouseLook();
        }
        CameraBob();
    }


    private void CameraBob()
    {
        Vector3 pos = Vector3.zero;

        if (cameraBob)
        {
            pos.y += Mathf.Lerp(pos.y, Mathf.Sin(Time.time * bobFrequency) * bobAmount * bobMultiplier * 1.4f, bobSmoothing * Time.deltaTime);
            pos.x += Mathf.Lerp(pos.x, Mathf.Cos(Time.time * bobFrequency / 2f) * bobAmount * bobMultiplier * 1.6f, bobSmoothing * Time.deltaTime);

            float inputMagnitude = characterController.velocity.magnitude;

            if (inputMagnitude > magnitudeThreshold && movement.canMove)
            {
                bobMultiplier = Mathf.Lerp(bobMultiplier, walkBobMultiplier, Time.deltaTime * walkReturnSpeed);
            }
            else
            {
                bobMultiplier = Mathf.Lerp(bobMultiplier, 1, Time.deltaTime * walkReturnSpeed);
                camHolder.transform.localPosition = Vector3.Lerp(camHolder.transform.localPosition, startingBobPos, Time.deltaTime * walkReturnSpeed);
            }

        }

        camHolder.transform.position += pos;
    }

    private void XMouseLook()
    {
        if (!canLook)
        {
            return;
        }

        float mouseX = Input.GetAxis("Mouse X") * sensitivity * Time.deltaTime;
        yRot += mouseX;
        if (limitX)
        {
            yRot = Mathf.Clamp(yRot, minX, maxX);
        }
        else
        {
            NormalizeYRotation();
        }

        transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, yRot + yOffset, 0), smoothing * Time.deltaTime);
    }

    private void NormalizeYRotation()
    {
        if (yRot > 180)
        {
            yRot -= 360;
        }
        if (yRot < -180)
        {
            yRot += 360;
        }
    }

    private void YMouseLook()
    {
        if (!canLook)
        {
            return;
        }

        float mouseY = Input.GetAxis("Mouse Y") * sensitivity * Time.deltaTime;
        xRot -= mouseY;
        xRot = Mathf.Clamp(xRot, minY, maxY);
        cam.transform.rotation = Quaternion.Slerp(cam.transform.rotation, Quaternion.Euler(xRot, cam.transform.eulerAngles.y, 0), smoothing * Time.deltaTime);
    }

    public void FreezeCamera(bool freeze)
    {
        if (freeze)
        {
            frozen = true;
        }
        else
        {
            ResetMouseLookRotation();
            frozen = false;
        }
    }


    public void LookAt(Transform obj)
    {
        StartCoroutine(LookAtCoroutine(obj));
    }

    private void ResetMouseLookRotation()
    {
        xRot = cam.transform.localEulerAngles.x;
        yRot = transform.localEulerAngles.y;

        if (xRot > 180) xRot -= 360;
        if (yRot > 180) yRot -= 360;
    }

    public void LimitXAxis(bool limit)
    {
        NormalizeYRotation();
        limitX = limit;
        yRot = Mathf.Clamp(yRot, minX, maxX);
        ResetMouseLookRotation();
    }


    private IEnumerator LookAtCoroutine(Transform obj)
    {
        canLook = false;
        isAnimating = true;
        float elapsedTime = 0;
        float progress;

        Quaternion startCamRot = cam.transform.rotation;
        Quaternion startTransformRot = transform.rotation;

        Vector3 directionToTarget = (obj.position - transform.position).normalized;
        Quaternion targetTransformRot = Quaternion.LookRotation(new Vector3(directionToTarget.x, 0, directionToTarget.z));
        Quaternion targetCamRot = Quaternion.LookRotation(directionToTarget);

        while (elapsedTime < rotationTime)
        {
            progress = Mathf.Clamp01(elapsedTime / rotationTime);

            transform.rotation = Quaternion.Slerp(startTransformRot, targetTransformRot, progress);
            cam.transform.rotation = Quaternion.Slerp(startCamRot, targetCamRot, progress);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.rotation = targetTransformRot;
        cam.transform.rotation = targetCamRot;
        ResetMouseLookRotation();
        isAnimating = false;

        if (!monologue.isMonologuing)
        {
            canLook = true;
        }
    }

    public void RotateCamera(Vector3 eulerRotation)
    {
        StartCoroutine(RotateCameraCoroutine(eulerRotation));
    }

    public void RotateCameraInstantly(Transform reference)
    {

        Quaternion targetTransformRot = Quaternion.Euler(transform.eulerAngles.x, reference.eulerAngles.y, transform.eulerAngles.z);
        Quaternion targetCamRot = Quaternion.Euler(reference.eulerAngles.x, cam.transform.eulerAngles.y, cam.transform.eulerAngles.z);

        transform.rotation = targetTransformRot;
        cam.transform.rotation = targetCamRot;
        ResetMouseLookRotation();
    }

    public void MoveCameraInstantly(Transform reference)
    {
        cam.transform.rotation = reference.rotation;
        cam.transform.position = reference.position;
        ResetMouseLookRotation();
    }

    public void SetMoveTime(float time)
    {
        cameraMoveTime = time;
    }

    private IEnumerator RotateCameraCoroutine(Vector3 rotation)
    {
        canLook = false;
        float elapsedTime = 0;
        float progress;
        isAnimating = true;

        Quaternion startCamRot = cam.transform.rotation;
        Quaternion startTransformRot = transform.rotation;

        Quaternion targetTransformRot = Quaternion.Euler(transform.eulerAngles.x, rotation.y, transform.eulerAngles.z);
        Quaternion targetCamRot = Quaternion.Euler(rotation.x, cam.transform.eulerAngles.y, cam.transform.eulerAngles.z);

        while (elapsedTime < rotationTime)
        {
            progress = Mathf.Clamp01(elapsedTime / rotationTime);

            transform.rotation = Quaternion.Slerp(startTransformRot, targetTransformRot, progress);
            cam.transform.rotation = Quaternion.Slerp(startCamRot, targetCamRot, progress);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        transform.rotation = targetTransformRot;
        cam.transform.rotation = targetCamRot;
        ResetMouseLookRotation();
        canLook = true;
        isAnimating = false;
    }

    public void MoveCamera(Transform reference)
    {
        StartCoroutine(MoveCameraCoroutine(reference.position, new Vector3(reference.eulerAngles.x, reference.eulerAngles.y, reference.eulerAngles.z)));
    }

    private IEnumerator MoveCameraCoroutine(Vector3 pos, Vector3 rotEuler)
    {
        canLook = false;
        isAnimating = true;
        float elapsedTime = 0;
        float progress;

        Quaternion startCamRot = cam.transform.rotation;
        Vector3 startCamPos = cam.transform.position;

        Quaternion targetCamRot = Quaternion.Euler(rotEuler.x, rotEuler.y, rotEuler.z);

        while (elapsedTime < cameraMoveTime)
        {
            progress = Mathf.Clamp01(elapsedTime / cameraMoveTime);

            cam.transform.rotation = Quaternion.Slerp(startCamRot, targetCamRot, progress);
            cam.transform.position = Vector3.Slerp(startCamPos, pos, progress);

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        cam.transform.rotation = targetCamRot;
        cam.transform.position = pos;
        ResetMouseLookRotation();
        canLook = true;
        isAnimating = false;
    }


}

