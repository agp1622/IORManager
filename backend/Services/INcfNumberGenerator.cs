namespace IORManager.Services;

public interface INcfNumberGenerator
{
    string GenerateNextNumber(string categoryCode);
    string PeekNextNumber(string categoryCode);
}
