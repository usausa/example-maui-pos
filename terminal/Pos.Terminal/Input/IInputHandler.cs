namespace Pos.Terminal.Input;

public interface IInputHandler
{
    bool Handle(ShortcutKey key);

    VisualElement? FindFocused();
}
