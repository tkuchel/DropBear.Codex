namespace DropBear.Codex.Utilities.Interfaces;

public interface IComparisonStrategy
{
    bool CanCompare(Type type);
    double Compare(object value1, object value2);
}
