using UnityEngine;

[DisallowMultipleComponent]
public class CameraPanController : MonoBehaviour
{
    [SerializeField] private float panSpeed = 6f;

    private PlayerInputAuthority _input;
    private TopDownFollowCamera _follow;

    private Vector2 _panAxis;
    private Vector3 _panOffset;

    private void Awake()
    {
        _follow = GetComponent<TopDownFollowCamera>();
        if (_follow == null)
            _follow = GetComponentInChildren<TopDownFollowCamera>();

        // Find input authority robustly (works even if Player tag isn't set)
        _input = FindFirstObjectByType<PlayerInputAuthority>();

        // Fallback: try Player tag
        if (_input == null)
        {
            var player = GameObject.FindWithTag("Player");
            if (player != null) _input = player.GetComponent<PlayerInputAuthority>();
        }
    }

    private void OnEnable()
    {
        if (_input != null) _input.CameraPan += OnPan;
    }

    private void OnDisable()
    {
        if (_input != null) _input.CameraPan -= OnPan;
    }

    private void Update()
    {
        // Pan offset integrates over time
        var delta = new Vector3(_panAxis.x, 0f, _panAxis.y) * (panSpeed * Time.deltaTime);
        _panOffset += delta;

        if (_follow != null)
            _follow.SetPanOffset(_panOffset);
    }

    private void OnPan(Vector2 v) => _panAxis = v;
}
