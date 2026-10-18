namespace SpaceTravel.Core;

public interface ISpaceScene : IDisposable
{
    string Name { get; }
    string Subtitle { get; }
    float DefaultDurationSeconds { get; }

    void Load(SceneContext ctx);
    void OnEnter(SceneContext ctx);
    void Update(SceneContext ctx);
    void Render(SceneContext ctx);
}
