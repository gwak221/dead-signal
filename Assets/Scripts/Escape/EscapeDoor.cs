using UnityEngine;

public class EscapeDoor : MonoBehaviour
{
    [SerializeField] float openHeight = 4f;
    [SerializeField] float openSpeed = 2f;

    Vector3 _closedLocalPos;
    Vector3 _openLocalPos;
    bool _isOpen;
    float _openT;

    void Awake()
    {
        _closedLocalPos = transform.localPosition;
        _openLocalPos = _closedLocalPos + Vector3.up * openHeight;
    }

    public void Open()
    {
        _isOpen = true;
    }

    void Update()
    {
        if (!_isOpen) return;

        _openT = Mathf.MoveTowards(_openT, 1f, openSpeed * Time.deltaTime);
        transform.localPosition = Vector3.Lerp(_closedLocalPos, _openLocalPos, _openT);
    }
}
