namespace IORManager.Services;

public interface INcfNumberGenerator
{
    string GenerateNextNumber();
    string PeekNextNumber();
}
