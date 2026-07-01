// Polyfill for netstandard2.0 to support record types and init-only setters
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
