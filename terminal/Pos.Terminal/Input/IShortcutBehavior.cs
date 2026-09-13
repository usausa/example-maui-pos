namespace Pos.Terminal.Input;

public interface IShortcutBehavior
{
    bool Handle(ShortcutKey key);
}
