namespace SpaceTravel.Core;

public sealed class SceneSequencer : IDisposable
{
    private readonly List<ISpaceScene> _scenes = new();
    private int _currentIndex;
    private bool _switchedDuringTransition;

    public bool AutoAdvance { get; set; } = true;
    public float TransitionProgress { get; private set; } // 0 = normal, 0..1 = transition envelope
    public bool IsTransitioning { get; private set; }
    private float _transitionTimer;
    private const float TransitionDuration = 1.1f;
    private int _pendingIndex = -1;

    public IReadOnlyList<ISpaceScene> Scenes => _scenes;
    public int CurrentIndex => _currentIndex;
    public ISpaceScene CurrentScene => _scenes[_currentIndex];

    public void RegisterScene(ISpaceScene scene, SceneContext ctx)
    {
        scene.Load(ctx);
        _scenes.Add(scene);
        if (_scenes.Count == 1)
        {
            scene.OnEnter(ctx);
        }
    }

    public void TriggerTransitionTo(int index, SceneContext ctx)
    {
        if (_scenes.Count == 0) return;
        int wrapped = ((index % _scenes.Count) + _scenes.Count) % _scenes.Count;
        if (wrapped == _currentIndex && !IsTransitioning)
        {
            ctx.SceneTime = 0f;
            CurrentScene.OnEnter(ctx);
            return;
        }

        _pendingIndex = wrapped;
        IsTransitioning = true;
        _transitionTimer = 0f;
        _switchedDuringTransition = false;
    }

    public void NextScene(SceneContext ctx) => TriggerTransitionTo(_currentIndex + 1, ctx);
    public void PreviousScene(SceneContext ctx) => TriggerTransitionTo(_currentIndex - 1, ctx);

    public void Update(SceneContext ctx)
    {
        if (_scenes.Count == 0) return;

        ctx.SceneTime += ctx.DeltaTime;

        if (!IsTransitioning && AutoAdvance && ctx.SceneTime >= CurrentScene.DefaultDurationSeconds)
        {
            NextScene(ctx);
        }

        if (IsTransitioning)
        {
            _transitionTimer += ctx.DeltaTime;
            float norm = Math.Clamp(_transitionTimer / TransitionDuration, 0f, 1f);

            // Triangle / smoothstep pulse peaking at 0.5
            TransitionProgress = MathF.Sin(norm * MathF.PI);

            if (norm >= 0.5f && !_switchedDuringTransition && _pendingIndex >= 0)
            {
                _currentIndex = _pendingIndex;
                ctx.SceneTime = 0f;
                CurrentScene.OnEnter(ctx);
                _switchedDuringTransition = true;
            }

            if (norm >= 1.0f)
            {
                IsTransitioning = false;
                TransitionProgress = 0f;
                _pendingIndex = -1;
            }
        }
        else
        {
            TransitionProgress = 0f;
        }

        CurrentScene.Update(ctx);
    }

    public void Render(SceneContext ctx)
    {
        if (_scenes.Count == 0) return;
        CurrentScene.Render(ctx);
    }

    public void Dispose()
    {
        foreach (var scene in _scenes)
        {
            scene.Dispose();
        }
        _scenes.Clear();
    }
}
